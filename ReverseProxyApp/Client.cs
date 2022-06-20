namespace ReverseProxyApp
{
    public sealed class Client
    {
        // The Singleton's instance is stored in a static field. There there are
        // multiple ways to initialize this field, all of them have various pros
        // and cons. However, doesn't work really well in multithreaded program.
        private static  HttpClient _client;

        private Client() { }      
      
        // This is the static method that controls the access to the singleton
        // instance. On the first run, it creates a singleton object and places
        // it into the static field. On subsequent runs, it returns the client
        // existing object stored in the static field.
        public static HttpClient GetInstance()
        {
            if (_client == null)
            {
                _client = new HttpClient();
            }
            return _client;
        }

    }
}
