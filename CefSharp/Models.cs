using System;
using System.Collections.Generic;
using System.Text;

namespace CefSharp
{
    public class Result
    {
        public List<Car> cars { get; set; }
    }
    public class Car
    {      
        public string stockType { get; set; }
        public string title { get; set; }
        public string mileage { get; set; }
        public string primaryPrice { get; set; }
        public string freeCarfax { get; set; }
        public string dealerName { get; set; }
        public string ratingStar { get; set; }
        public string review { get; set; }
        public string milesFrom { get; set; }
        public bool hasHomeDelivery { get; set; }
        public List<HomeDelivery> HomeDeliveries { get; set; }

        public Car()
        {
            HomeDeliveries = new List<HomeDelivery>();
        }
    }

    public class HomeDelivery
    {
        public string header { get; set; }
        public string description { get; set; }
    }
}
