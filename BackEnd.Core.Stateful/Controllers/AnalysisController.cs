using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace BackEnd.Core.Stateful.Controllers
{
    [Route("api/[controller]")]
    public class AnalysisController : Controller
    {
        private readonly IReliableStateManager _stateManager;

        public AnalysisController(
            IReliableStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        [HttpPost]
        public async Task MergeSurveyAnswerToAnalysisAsync([FromBody]string slot, [FromBody]string response)
        {
            try
            {
                var reliableDictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, int>>("reliableDictionary");

                using (var tx = _stateManager.CreateTransaction())
                {
                    var conditional = await reliableDictionary.TryGetValueAsync(tx, slot);
                    var accumulated = 0;

                    if (!conditional.HasValue)
                    {
                        accumulated += 1;
                    }
                    else
                    {
                        accumulated = conditional.Value + 1;
                    }

                    ServiceEventSource.Current.Message("Slug name:{0}|Total answers:{1}", slot,
                        accumulated);

                    await reliableDictionary.AddOrUpdateAsync(tx, slot, accumulated, (key, element) => {
                        return element;
                    });

                    await tx.CommitAsync();
                }
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
                var reliableDictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, int>>("reliableDictionary");
                var result = 0;

                using (var tx = _stateManager.CreateTransaction())
                {
                    var conditional = await reliableDictionary.TryGetValueAsync(tx, slot);

                    if (conditional.HasValue)
                    {
                        result = conditional.Value;
                    }
                    await tx.CommitAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.ServiceRequestFailed(ex.ToString());
                throw;
            }
        }
    }
}
