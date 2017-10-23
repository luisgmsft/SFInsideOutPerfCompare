using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Client.BackEnd;
using Domain;

namespace FrontEnd.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IBackEndService backEnd;

        public IndexModel(IBackEndService backEnd)
        {
            this.backEnd = backEnd;
        }
        public async Task OnGet()
        {
            this.Count = await backEnd.GetSummaryAsync("luigi");
        }

        [BindProperty]
        public SlotModel SlotModel { get; set; }
        public int Count { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                await backEnd.MergeAsync(SlotModel.Slot);

                //Count = await backEnd.GetSummaryAsync("sample-slot");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
