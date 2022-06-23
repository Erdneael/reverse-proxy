using System.Collections.Generic;

namespace ReverseProxyApp
{
    //Class representing a LoadBalancer that can implements several load-balancing strategies
    public class LoadBalancer
    {
        //List of string representing our downstreams in our case adresses of servers
        private readonly IList<string> downstreams;
        //Size of the List
        private int _size;
        //Current position for the round-robin strategie
        private int _position;
        //The last index
        private int _last;

        //Object use to implement a thread safe version of the algorithm
        private readonly object _lock;

        //Constructor of the LoadBalancer
        public LoadBalancer()
        {
            //A better way if implementation would be to use maybe a configuration file to initialize the List
            downstreams = new List<string>();
            downstreams.Add("http://localhost:5118");
            downstreams.Add("https://localhost:7069");

            _size = downstreams.Count;
            _lock = new object();
            _last = 0;
        }

        public Uri getUri(string value)
        {
            //If only one downtream returning the Uri corresponding
            if (_size == 1)
            {
                return new Uri(downstreams[0] + value);
            }

            lock (_lock)
            {
                //If last downstream reset the index to the begining of the list
                if (_position == _size)
                {
                    _position = 0;
                }

                //Css content is send on a different request after the html so in our case when send a second request to the same server to have matching css
                if (value.Contains(".css"))
                {
                    return new Uri(downstreams[_last] + value);
                }
                else
                {

                    _last = _position;
                    return new Uri(downstreams[_position++] + value);
                }
            }
        }
    }
}
