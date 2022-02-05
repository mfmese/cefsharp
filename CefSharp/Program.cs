using CefSharp.OffScreen;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CefSharp
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            const string mainUrl = "https://www.cars.com/signin/?redirect_path=%2F";

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

                // Create the CefSharp.OffScreen.ChromiumWebBrowser instance
                using (var browser = new ChromiumWebBrowser(mainUrl))
                {
                    var initialLoadResponse = await browser.WaitForInitialLoadAsync();

                    if (!initialLoadResponse.Success)
                    {
                        throw new Exception(string.Format("Page load failed with ErrorCode:{0}, HttpStatusCode:{1}", initialLoadResponse.ErrorCode, initialLoadResponse.HttpStatusCode));
                    }

                    _ = await browser.EvaluateScriptAsync("document.querySelector('[id=email]').value = 'johngerson808@gmail.com'");
                    _ = await browser.EvaluateScriptAsync("document.querySelector('[id=password]').value = 'test8008'");
                    _ = await browser.EvaluateScriptAsync("document.querySelector('.sds-button').click();");

                    _ = await browser.EvaluateScriptAsync("(function(){ document.getElementsByClassName('sds-button')[0].click(); })();");
                    _ = await browser.EvaluateScriptAsync("(function(){ document.getElementsByName('stock_type')[0].value = 'used' })();");
                    _ = await browser.EvaluateScriptAsync("(function(){ document.getElementsByName('makes[]')[0].value = 'Tesla' })();");
                    _ = await browser.EvaluateScriptAsync("(function(){ document.getElementsByName('models[]')[0].value = 'All models' })();");
                    _ = await browser.EvaluateScriptAsync("(function(){ document.getElementsByName('list_price_max')[0].value = '10000' })();");
                    _ = await browser.EvaluateScriptAsync("(function(){ document.getElementsByName('maximum_distance')[0].value = 'all' })();");
                    _ = await browser.EvaluateScriptAsync("(function(){ document.getElementsByName('zip')[0].value = '94596' })();");
                    _ = await browser.EvaluateScriptAsync("document.querySelector('.sds-button').click();");



                    var pageAddress1 = string.Empty;
                    var pageAddress2 = string.Empty;
                    var host = "https://www.cars.com";
                    using (var browser2 = new ChromiumWebBrowser(host  + "/shopping/results/?stock_type=used&makes%5B%5D=tesla&models%5B%5D=&list_price_max=&maximum_distance=all&zip=94596"))
                    {
                        var initialLoadResponse2 = await browser2.WaitForInitialLoadAsync();

                        pageAddress1 = browser2.Address;

                        var pageAddressResponse = await browser2.EvaluateScriptAsync("document.querySelector('[aria-label=\"Go to Page 2\"]').href");
                        pageAddress2 = pageAddressResponse?.Result.ToString();
                    }

                    var resultString = string.Empty;
                    using (var browser2 = new ChromiumWebBrowser( pageAddress1))
                    {
                        var initialLoadResponse2 = await browser2.WaitForInitialLoadAsync();

                        const string script = @"(function()
                        {
                          let elements = document.querySelector('#vehicle-cards-container').querySelectorAll(':scope > .vehicle-card');

                          let list = [];
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

                                     
                        JavascriptResponse response7 = await browser2.EvaluateScriptAsync(script);
                        dynamic rr = response7.Result;
                        string jsonString = JsonConvert.SerializeObject(rr);

                        var cars = JsonConvert.DeserializeObject<List<Car>>(jsonString);

                        int i = 0;
                        foreach (var car in cars)
                        {

                            if (!car.hasHomeDelivery)
                            {
                                continue;
                            }                         

                            var response = await browser2.EvaluateScriptAsync($"document.querySelector('#vehicle-cards-container').querySelectorAll(':scope > .vehicle-card')[{i}].querySelector('.sds-badge--home-delivery').click()");

                            Thread.Sleep(500);
                            string homeDeliveryScript = @"(function()
                                    {   
                                       var elements = document.querySelectorAll('.sds-modal__content-body')[2].querySelectorAll('li');

                                        let list = [];
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
                            JavascriptResponse response8 = await browser2.EvaluateScriptAsync(homeDeliveryScript);
                            dynamic rr2 = response8?.Result;
                            if (rr2 == null)
                                continue;

                            string jsonString2 = JsonConvert.SerializeObject(rr2);
                            var homeDeliveries = JsonConvert.DeserializeObject<List<HomeDelivery>>(jsonString2);
                            car.HomeDeliveries.AddRange(homeDeliveries);


                            i++;
                        }

                        var jsonResult = JsonConvert.SerializeObject(cars);

                        File.WriteAllText("c:/temp/cefsharpresult.text", jsonResult);

                    }


                    Console.WriteLine("Image viewer launched. Press any key to exit.");
                }

                // Wait for user to press a key before exit
                Console.ReadKey();

                // Clean up Chromium objects. You need to call this in your application otherwise
                // you will get a crash when closing.
                Cef.Shutdown();
            });

            return 0;
        }
       
    }
}
