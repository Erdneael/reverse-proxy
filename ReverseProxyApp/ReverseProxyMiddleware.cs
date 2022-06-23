using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using ReverseProxyApp;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ReverseProxyApplication
{
    public class ReverseProxyMiddleware
    {
        //Static instance that represent a HttpClient, class contained in System.Net.Http
        //and will send and processed the requests
        private static readonly HttpClient _httpClient = Client.GetInstance();

        //Object representing the middleware, in our case to pass to the next middleware
        private readonly RequestDelegate _nextMiddleware;

        //Instance of the loadBalancer 
        private LoadBalancer loadBalancer;

        //In Memory cache object
        private readonly IMemoryCache _cache;

        //Simplified Semaphore for thread safe cache
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        //Constructor of the class, with dependencies injection initializing the "_nextMiddelware" and "_cache"
        public ReverseProxyMiddleware(RequestDelegate nextMiddleware, IMemoryCache memoryCache)
        {
            _nextMiddleware = nextMiddleware;
            loadBalancer = new LoadBalancer();
            _cache = memoryCache;
        }

        //Main method of the reverse proxy where everithing happend, is called at every client request
        public async Task Invoke(HttpContext context)
        {
            //Firt build the target Uri
            Uri targetUri = BuildTargetUri(context.Request);

            //If Uri not null
            if (targetUri != null)
            {
                //With the Uri create a HttpRequestMessage for the server
                var targetRequestMessage = CreateTargetMessage(context, targetUri);
                //Send the request and use the response
                using (var responseMessage = await _httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
                {
                    context.Response.StatusCode = (int)responseMessage.StatusCode;
                    //Copy the http headers and response
                    CopyFromTargetResponseHeaders(context, responseMessage);
                    //Send the response to the client
                    await SendResponceToClient(context, responseMessage);
                }


                //Way of improvement would be to have a shared cahce using a database
                if (_cache.TryGetValue(targetRequestMessage.RequestUri, out string respo))
                {
                    /*  
                     if Something is in cache do something with it  ....
                     */

                }
                else
                {
                    try
                    {
                        //Wait for the Semaphore to be thread safe
                        await semaphore.WaitAsync();
                        if (_cache.TryGetValue(targetRequestMessage.RequestUri, out string resp))
                        {
                            /*
                             *Check if the cache is available if yes then use it 
                             */
                        }
                        else
                        {
                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                      .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                                      .SetAbsoluteExpiration(TimeSpan.FromSeconds(3600))
                                      .SetPriority(CacheItemPriority.Normal);

                            //Set some information in the cache with the key corresponding to the request Uri
                            _cache.Set(targetRequestMessage.RequestUri, "Some information", cacheEntryOptions);
                        }

                    }
                    finally
                    {
                        semaphore.Release();
                    }
                    return;
                }

                await _nextMiddleware(context);
            }
        }


        private async Task SendResponceToClient(HttpContext context, HttpResponseMessage responseMessage)
        {
            var content = await responseMessage.Content.ReadAsByteArrayAsync();

            //If HTML or JS encode to prevent security failure
            if (IsContentOfType(responseMessage, "text/html") ||
                IsContentOfType(responseMessage, "text/javascript"))
            {
                var stringContent = Encoding.UTF8.GetString(content);

                //Write the response to the client 
                await context.Response.WriteAsync(stringContent, Encoding.UTF8);
            }
            else
            {
                await context.Response.Body.WriteAsync(content);
            }
        }

        private bool IsContentOfType(HttpResponseMessage responseMessage, string type)
        {
            var result = false;

            if (responseMessage.Content?.Headers?.ContentType != null)
            {
                result = responseMessage.Content.Headers.ContentType.MediaType == type;
            }

            return result;
        }

        private HttpRequestMessage CreateTargetMessage(HttpContext context, Uri targetUri)
        {
            var requestMessage = new HttpRequestMessage();

            CopyFromOriginalRequestContentAndHeaders(context, requestMessage);

            requestMessage.RequestUri = targetUri;
            requestMessage.Headers.Host = targetUri.Host;
            requestMessage.Method = GetHTTPMethodFromString(context.Request.Method);

            return requestMessage;
        }

        //Copy the client request
        private void CopyFromOriginalRequestContentAndHeaders(HttpContext context, HttpRequestMessage requestMessage)
        {
            var requestMethod = context.Request.Method;

            if (!HttpMethods.IsGet(requestMethod) &&
              !HttpMethods.IsHead(requestMethod) &&
              !HttpMethods.IsDelete(requestMethod) &&
              !HttpMethods.IsTrace(requestMethod))
            {
                //If a HttpMethod create a StreamContent and initialize the requeste Message
                var streamContent = new StreamContent(context.Request.Body);
                requestMessage.Content = streamContent;
            }

            //Copy the header of the client request
            foreach (var header in context.Request.Headers)
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        //Copy the headers of the response to the header of the actual HttpContext
        private void CopyFromTargetResponseHeaders(HttpContext context, HttpResponseMessage responseMessage)
        {

            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            //Remove the encoding
            context.Response.Headers.Remove("transfer-encoding");
        }

        //Method returning the HttpMethod of the string
        private static HttpMethod GetHTTPMethodFromString(string method)
        {
            if (HttpMethods.IsDelete(method)) return HttpMethod.Delete;
            if (HttpMethods.IsGet(method)) return HttpMethod.Get;
            if (HttpMethods.IsHead(method)) return HttpMethod.Head;
            if (HttpMethods.IsOptions(method)) return HttpMethod.Options;
            if (HttpMethods.IsPost(method)) return HttpMethod.Post;
            if (HttpMethods.IsPut(method)) return HttpMethod.Put;
            if (HttpMethods.IsTrace(method)) return HttpMethod.Trace;
            return new HttpMethod(method);
        }


        //Method returning the target Uri and using a LoadBalancer  
        private Uri BuildTargetUri(HttpRequest request)
        {
            Uri targetUri = null;

            if (request.Path.HasValue)
            {
                /*
                 * Basic implementation of a random load-balancer between 2 servers
                 *   Random random = new Random();
                 *   int test = random.Next(0,2);
                 *   if (test==0)
                 *   {
                 *      targetUri = new Uri("http://localhost:5118" + request.Path.Value);
                 *   }
                 *   else
                 *   {
                 *       targetUri = new Uri("https://localhost:7069" + request.Path.Value);
                 *   }
                 * 
                 */
                //Round-robin strategy load balancer
                //An improvement that could be done is a weighted load balancing strategy instead of a round robin 
                targetUri = loadBalancer.getUri(request.Path.Value);
            }
            return targetUri;
        }
    }
}