using CefSharp.OffScreen;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CefSharp
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            const string mainUrl = "https://www.cars.com";

            Console.WriteLine("This application will load {0}. Please wait...", mainUrl);
            Console.WriteLine();
            
            AsyncContext.Run(async delegate
            {
                var settings = new CefSettings()
                {
                    CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CefSharp\\Cache")
                };

                var success = await Cef.InitializeAsync(settings, performDependencyCheck: true, browserProcessHandler: null);
                if (!success)
                {
                    throw new Exception("Unable to initialize CEF, check the log file.");
                }

                var signIn = new SignIn
                {
                    Email = "johngerson808@gmail.com",
                    Password = "test8008"
                };

                var searchFilter = new SearchFilter
                {
                    stockType = "used",
                    makes = "tesla",
                    models = "All models",
                    listPriceMax = "100000",
                    maximumDistance = "all",
                    zip = "94596"
                };

                using (var browser = new ChromiumWebBrowser(mainUrl))
                {
                    var initialLoadResponse = await browser.WaitForInitialLoadAsync();
                    if (!initialLoadResponse.Success)
                    {
                        log(string.Format("Page load failed with ErrorCode:{0}, HttpStatusCode:{1}", initialLoadResponse.ErrorCode, initialLoadResponse.HttpStatusCode));
                    }

                    await signInAsync(browser, signIn);    

                    await searchAsync(browser, searchFilter);
                }

                var addresses = await generateAddressAsync(searchFilter, mainUrl);

                var cars = await generateCarsAsync(addresses, searchFilter, mainUrl);

                searchFilter.models = await getSelectedModelFilterAsync(addresses.FirstOrDefault(), 0);

                var cars2 = await generateCarsAsync(addresses, searchFilter, mainUrl);

                cars.AddRange(cars2);

                var jsonResult = JsonConvert.SerializeObject(cars);

                writeToFileAndOpen(jsonResult);

                // Wait for user to press a key before exit
                Console.ReadKey();

                // Clean up Chromium objects. You need to call this in your application otherwise you will get a crash when closing.
                Cef.Shutdown();
            });

            return 0;
        }

        private async static Task signInAsync(ChromiumWebBrowser browser, SignIn signIn)
        {
            _ = await browser.EvaluateScriptAsync($"document.querySelector('[id=email]').value = '{signIn.Email}'");
            _ = await browser.EvaluateScriptAsync($"document.querySelector('[id=password]').value = '{signIn.Password}'");
            _ = await browser.EvaluateScriptAsync("document.querySelector('.sds-button').click();");
        }

        private async static Task searchAsync(ChromiumWebBrowser browser, SearchFilter filter)
        {
            _ = await browser.EvaluateScriptAsync($"document.getElementsByName('stock_type')[0].value = '{filter.stockType}'");
            _ = await browser.EvaluateScriptAsync($"document.getElementsByName('makes[]')[0].value = '{filter.makes}'");
            _ = await browser.EvaluateScriptAsync($"document.getElementsByName('models[]')[0].value = '{filter.models}'");
            _ = await browser.EvaluateScriptAsync($"document.getElementsByName('list_price_max')[0].value = '{filter.listPriceMax}'");
            _ = await browser.EvaluateScriptAsync($"document.getElementsByName('maximum_distance')[0].value = '{filter.maximumDistance}'");
            _ = await browser.EvaluateScriptAsync($"document.getElementsByName('zip')[0].value = '{filter.zip}'");

            _ = await browser.EvaluateScriptAsync("document.querySelector('.sds-button').click();");
        }

        private async static Task<List<Car>> generateCarsAsync(List<string> addresses, SearchFilter searchFilter, string mainUrl)
        {            
            var cars = new List<Car>();

            foreach (var address in addresses)
            {
                using (var browser = new ChromiumWebBrowser(address))
                {
                    var initialLoadResponse = await browser.WaitForInitialLoadAsync();
                    if (!initialLoadResponse.Success)
                    {
                        log(string.Format("Page load failed with ErrorCode:{0}, HttpStatusCode:{1}", initialLoadResponse.ErrorCode, initialLoadResponse.HttpStatusCode));
                    }

                    cars = await generateCarsAsync(browser);

                    int i = 0;
                    foreach (var car in cars)
                    {
                        if (!car.hasHomeDelivery)
                            continue;

                        var homeDeliveries = await generateHomeDelivery(browser, i);
                        if (homeDeliveries == null)
                            continue;

                        car.HomeDeliveries.AddRange(homeDeliveries);

                        i++;
                    }
                }
            }

            return cars;
        }

        private async static Task<List<string>> generateAddressAsync(SearchFilter searchFilter, string mainUrl)
        {
            var addresses = new List<string>();

            for (int i = 1; i < 3; i++)
            {
                using (var browser = new ChromiumWebBrowser(mainUrl + $"/shopping/results/?stock_type={searchFilter.stockType}&makes%5B%5D={searchFilter.makes}&models%5B%5D={searchFilter.models}&list_price_max={searchFilter.listPriceMax}&maximum_distance={searchFilter.maximumDistance}&zip={searchFilter.zip}"))
                {
                    var initialLoadResponse = await browser.WaitForInitialLoadAsync();
                    if (!initialLoadResponse.Success)
                    {
                        log(string.Format("Page load failed with ErrorCode:{0}, HttpStatusCode:{1}", initialLoadResponse.ErrorCode, initialLoadResponse.HttpStatusCode));
                    }

                    if (i == 1)
                    {
                        addresses.Add(browser.Address);
                        continue;
                    }

                    await browser.WaitForInitialLoadAsync();
                    var pageAddressResponse = await browser.EvaluateScriptAsync($"document.querySelector('[aria-label=\"Go to Page {i}\"]').href");
                    if (pageAddressResponse.Success)
                    {
                        var address = pageAddressResponse?.Result.ToString();
                        addresses.Add(address);
                    }                   
                }
            }

            return addresses;
        }

        private async static Task clickHomeDeliveryAsync(ChromiumWebBrowser browser, int index)
        {
            var clickPageNumber = $"document.querySelector('#vehicle-cards-container').querySelectorAll(':scope > .vehicle-card')[{index}].querySelector('.sds-badge--home-delivery').click()";
            await browser.EvaluateScriptAsync(clickPageNumber);
        }

        private async static Task<List<Car>> generateCarsAsync(ChromiumWebBrowser browser)
        {
            const string script = @"(function(){

                            let list = [];
                            let elements = document.querySelector('#vehicle-cards-container').querySelectorAll(':scope > .vehicle-card');
                            
                            for (let i = 0; i < elements.length; i++) {
                                let element = elements[i].querySelectorAll('.vehicle-details')[0];
                                let obj = {};    
                                obj.stockType = element.querySelector('.stock-type').innerText;
                                obj.title = element.querySelector('.title').innerText;
                                obj.mileage = element.querySelector('.mileage').innerText;
                                obj.primaryPrice = element.querySelector('.primary-price').innerText;
                                obj.freeCarfax = element.querySelector('.sds-link--ext').innerText;
                                obj.dealerName = element.querySelector('.dealer-name').innerText;
                                obj.ratingStar = element.querySelector('.sds-rating__count').innerText;
                                obj.review = element.querySelector('.sds-rating__link').innerText;
                                obj.milesFrom = element.querySelector('.miles-from').innerText;
                                obj.hasHomeDelivery = false;

                                if(element.querySelector('.sds-badge--home-delivery') !== null){
                                    obj.hasHomeDelivery = true;
                                }
                              
                                list.push(obj);
                            };                          

                            return list;
                        })();";


            JavascriptResponse response = await browser.EvaluateScriptAsync(script);
            if (!response.Success)
                return null;

            dynamic result = response.Result;
            string jsonString = JsonConvert.SerializeObject(result);
            var cars = JsonConvert.DeserializeObject<List<Car>>(jsonString);

            return cars;
        }

        private async static Task<List<HomeDelivery>> generateHomeDelivery(ChromiumWebBrowser browser, int index)
        {

            await clickHomeDeliveryAsync(browser, index);

            Thread.Sleep(500); //Modal loading here...

            string homeDeliveryScript = @"(function(){

                                        let list = [];
                                        var elements = document.querySelectorAll('.sds-modal__content-body')[2].querySelectorAll('li');
                                        
                                        for (let i = 0; i < elements.length; i++) {
                                            let element = elements[i];                                
                                            if(element.innerText != ''){
                                                if(element.querySelector('.sds-badge__label') !== null){
                                                    let obj = {}; 
                                                    obj.header = element.querySelector('.sds-badge__label').innerText
                                                    obj.description = element.querySelector('.badge-description').innerText
                                                    list.push(obj);
                                                }
                                            }
                                        }
                              
                                        return list;
                                    })();";

            JavascriptResponse response = await browser.EvaluateScriptAsync(homeDeliveryScript);
            if (!response.Success)
                return null;

            dynamic result = response?.Result;
            string jsonString = JsonConvert.SerializeObject(result);
            var homeDeliveries = JsonConvert.DeserializeObject<List<HomeDelivery>>(jsonString);

            return homeDeliveries;
        }

        private static async Task<string> getSelectedModelFilterAsync(string url, int index)
        {
            using (var browser = new ChromiumWebBrowser(url))
            {
                var initialLoadResponse = await browser.WaitForInitialLoadAsync();
                if (!initialLoadResponse.Success)
                {
                    log(string.Format("Page load failed with ErrorCode:{0}, HttpStatusCode:{1}", initialLoadResponse.ErrorCode, initialLoadResponse.HttpStatusCode));
                }

                var script = $"var model = document.querySelectorAll('.refinement-simple')[0].querySelectorAll('.sds-input')[{index}].value; return model;";

                JavascriptResponse response = await browser.EvaluateScriptAsync(script);
                if (!response.Success)
                    return null;

                return (string)response.Result;
            }

            
        }

        private static void writeToFileAndOpen(string jsonResult)
        {
            var executionPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            bool exists = System.IO.Directory.Exists(executionPath + "/exportfile");
            if (!exists)
                System.IO.Directory.CreateDirectory(executionPath + "/exportfile");

            var filePath = $"{executionPath}/exportfile/cefsharpresult.json";
            File.WriteAllText(filePath, jsonResult);

            Process.Start("notepad.exe", filePath);

            log(string.Format("File saved successfully to path: {0}", filePath));
        }

        private static void log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
