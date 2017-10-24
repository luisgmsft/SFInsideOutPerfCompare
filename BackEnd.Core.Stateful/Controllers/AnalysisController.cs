using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Domain;

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
        public async Task MergeSurveyAnswerToAnalysisAsync([FromBody]SlotModel model)
        {
            try
            {
                var reliableDictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, int>>("reliableDictionary");

                using (var tx = _stateManager.CreateTransaction())
                {
                    var conditional = await reliableDictionary.TryGetValueAsync(tx, model.Slot, LockMode.Update);
                    var accumulated = 0;

                    if (!conditional.HasValue)
                    {
                        accumulated += 1;
                    }
                    else
                    {
                        accumulated = conditional.Value + 1;
                    }

                    ServiceEventSource.Current.Message($"Slug name:{model.Slot}|Total answers:{accumulated}");

                    await reliableDictionary.AddOrUpdateAsync(tx, model.Slot, accumulated, (key, element) => {
                        return accumulated;
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
