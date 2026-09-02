using Scadex.DataAccess.Abstract;
using Scadex.DataAccess.Contexts;
using Scadex.DataAccess.Repository;
using Scadex.Model.Entities;

namespace Scadex.DataAccess.Concrete;

public class CompanyRepository : RepositoryBase<Company, AppDbContext>, ICompanyRepository
{
    public CompanyRepository(AppDbContext context) : base(context)
    {
    }
}