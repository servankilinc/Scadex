using Scadex.Core.BaseRequestModels;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.Pagination;
using Scadex.Core.Utils.ResultPattern;
using Scadex.Model.Dtos.Common;
using Scadex.Model.Dtos.User.Commands;
using Scadex.Model.Dtos.User.Queries;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.Business.Abstract;

public interface IUserService
{
    // Get
    Task<Result<User>> GetAsync(Expression<Func<User, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<User>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<UserBaseDto>> GetBaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<UserDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    // List
    Task<Result<ICollection<User>>> GetListAsync(Expression<Func<User, bool>> where, CancellationToken cancellationToken = default);
    Task<Result<ICollection<User>>> GetListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<UserBaseDto>>> GetBaseListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);
    Task<Result<ICollection<UserDetailDto>>> GetDetailListAsync(DynamicRequest? request = default, CancellationToken cancellationToken = default);

    // SelectList
    Task<Result<ICollection<SelectItemDto>>> SelectListAsync(Expression<Func<User, bool>>? where = default, CancellationToken cancellationToken = default);

    // Create
    Task<Result> CreateAsync(UserCreateDto request, CancellationToken cancellationToken = default);

    // Update
    Task<Result<UserUpdateDto>> GetUpdateModelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(UserUpdateDto request, CancellationToken cancellationToken = default);

    // Pagination / Datatable
    Task<Result<PaginationResponse<UserDetailDto>>> PaginationAsync(DynamicPaginationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseClientSide<UserDetailDto>>> DatatableClientSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
    Task<Result<DatatableResponseServerSide<UserDetailDto>>> DatatableServerSideAsync(DynamicDatatableRequest request, CancellationToken cancellationToken = default);
}