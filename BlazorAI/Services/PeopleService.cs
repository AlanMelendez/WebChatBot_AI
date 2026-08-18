using BlazorAI.Data;
using BlazorAI.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorAI.Services
{
    public class PeopleService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IPersonService
    {
        public async Task<IEnumerable<Person>> GetAll()
        {
            using var contex = dbContextFactory.CreateDbContext();
            return await contex.People.ToListAsync();
        }
    }
}
