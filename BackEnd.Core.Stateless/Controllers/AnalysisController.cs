using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Newtonsoft.Json;
using BackEnd.Core.Stateless.Helpers;
using Domain;

namespace BackEnd.Core.Stateless.Controllers
{
    [Route("api/[controller]")]
    public class AnalysisController : Controller
    {
        private readonly IConnectionMultiplexer connectionMultiplexer;

        public AnalysisController(IConnectionMultiplexer connectionMultiplexer)
        {
            this.connectionMultiplexer = connectionMultiplexer;
        }

        [HttpPost]
        public async Task MergeSurveyAnswerToAnalysisAsync([FromBody]SlotModel model)
        {
            try
            {
                var surveyAnswersSummaryCache = connectionMultiplexer.GetDatabase();
                var success = false;

                do
                {
                    var result = await surveyAnswersSummaryCache.StringGetAsync(model.Slot);
                    var isNew = result.IsNullOrEmpty;
                    var transaction = surveyAnswersSummaryCache.CreateTransaction();

                    var accumulated = 0;

                    if (isNew)
                    {
                        transaction.AddCondition(Condition.KeyNotExists(model.Slot));
                        accumulated += 1;
                    }
                    else
                    {
                        accumulated = (JsonConvert.DeserializeObject<int>(result)) + 1;
                        transaction.AddCondition(Condition.StringEqual(model.Slot, result));
                    }

                    ServiceEventSource.Current.Message($"Slug name:{model.Slot}|Total answers:{accumulated}");

                    transaction.StringSetAsync(model.Slot,
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
                var surveyAnswersSummaryCache = connectionMultiplexer.GetDatabase();

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
