using System.Collections.Generic;

namespace ReverseProxyApp
{
    public class LoadBalancer
    {
        private readonly IList<string> servers ;
        private int _size;
        private int _position;
        private readonly object _lock;

        public LoadBalancer()
        {
            servers = new List<string>();
            servers.Add("http://localhost:5118");
            servers.Add("https://localhost:7069");

            _size = servers.Count;
            _lock = new object();
        }

        public  Uri getUri(string value)
        {



            if (_size == 1)
            {
                return new Uri(servers[0]+value);
            }

            lock (_lock)
            {
                if (_position == _size)
                {
                    _position = 0;
                }
                return new Uri(servers[_position++]+value);
            }
        }
    }
}
