using System.Threading.Tasks;

namespace Client.BackEnd
{
    public interface IBackEndService
    {
        Task MergeAsync(string slot);

        Task<int> GetSummaryAsync(string slot);
    }
}
