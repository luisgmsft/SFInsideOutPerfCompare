using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Newtonsoft.Json;
using BackEnd.Core.Stateless.Helpers;

namespace BackEnd.Core.Stateless.Controllers
{
    [Route("api/[controller]")]
    public class AnalysisController : Controller
    {
        private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            string cacheConnection = ServiceFabricConfiguration.GetConfigurationSettingValue("ConnectionStrings", "RedisCacheConnectionString", "YourRedisCacheConnectionString");

            return ConnectionMultiplexer.Connect(cacheConnection);
        });

        public static ConnectionMultiplexer Connection
        {
            get
            {
                return lazyConnection.Value;
            }
        }

        [HttpPost]
        public async Task MergeSurveyAnswerToAnalysisAsync([FromBody]string slot, [FromBody]string response)
        {
            try
            {
                var surveyAnswersSummaryCache = Connection.GetDatabase();
                var success = false;

                do
                {
                    var result = await surveyAnswersSummaryCache.StringGetAsync(slot);
                    var isNew = result.IsNullOrEmpty;
                    var transaction = surveyAnswersSummaryCache.CreateTransaction();

                    var accumulated = 0;

                    if (isNew)
                    {
                        transaction.AddCondition(Condition.KeyNotExists(slot));
                        accumulated += 1;
                    }
                    else
                    {
                        accumulated = JsonConvert.DeserializeObject<int>(result);
                        transaction.AddCondition(Condition.StringEqual(slot, ++accumulated));
                    }

                    ServiceEventSource.Current.Message("Slug name:{0}|Total answers:{1}", slot,
                        accumulated);

                    transaction.StringSetAsync(slot,
                            JsonConvert.SerializeObject(accumulated));

                    //This is a simple implementation of optimistic concurrency.
                    //If transaction fails, another user must have edited the same survey.
                    //If so, try to process this survey answer again.

                    //Another approach is to store each survey answer option as a separate hash in Redis and simply increment
                    //the hash value. This technique will not cause collisions with other threads but will require a redesign of
                    //how survey answer summaries are stored in redis. This approach is left to the reader to implement.

                    success = await transaction.ExecuteAsync();
                } while (!success);

            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceRequestFailed(ex.ToString());
                throw;
            }
        }

        [HttpGet]
        [Route("summary/{slot}")]
        public async Task<int> GetSurveyAnswersSummaryAsync(string slot)
        {
            try
            {
                var surveyAnswersSummaryCache = Connection.GetDatabase();

                // Look for slug name in the survey answers summary cache
                var surveyAnswersSummaryInStore = await surveyAnswersSummaryCache.StringGetAsync(slot);

                var returnData = 0;

                if (!surveyAnswersSummaryInStore.IsNullOrEmpty)
                {
                    returnData =
                        JsonConvert.DeserializeObject<int>(surveyAnswersSummaryInStore);
                }

                return returnData;
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceRequestFailed(ex.ToString());
                throw;
            }
        }
    }
}
