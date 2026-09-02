using AutoMapper;
using Microsoft.EntityFrameworkCore.Query;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Model;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.DynamicQuery;
using Scadex.Core.Utils.Pagination;
using System.Linq.Expressions;

namespace Scadex.DataAccess.Repository;

public interface IRepositoryAsync<TEntity> where TEntity : IEntity
{
    #region Add
    Task<TEntity> AddAndSaveAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<ICollection<TEntity>> AddAndSaveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    #endregion

    #region Update
    Task<TEntity> UpdateAndSaveAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<ICollection<TEntity>> UpdateAndSaveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    #endregion

    #region Delete
    Task DeleteAndSaveAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteAndSaveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task<int> DeleteAndSaveAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default);
    #endregion
    
    #region Restore
    Task<int> RestoreAndSaveAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default);
    #endregion

    #region IsExist & Count
    Task<bool> IsExistAsync(
        Filter? filter = null,
        Expression<Func<TEntity, bool>>? where = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<int> CountAsync(
        Filter? filter = null,
        Expression<Func<TEntity, bool>>? where = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    #endregion

    #region Get
    Task<TEntity?> GetAsync(
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        bool tracking = true,
        CancellationToken cancellationToken = default
    );
    Task<TResult?> GetAsync<TResult>(
        Expression<Func<TEntity, TResult>> select,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<TResult?> GetAsync<TResult>(
        IConfigurationProvider configurationProvider,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    #endregion

    #region GetAll
    Task<ICollection<TEntity>?> GetAllAsync(
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        bool tracking = true,
        CancellationToken cancellationToken = default
    );
    Task<ICollection<TResult>?> GetAllAsync<TResult>(
        Expression<Func<TEntity, TResult>> select,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<ICollection<TResult>?> GetAllAsync<TResult>(
        IConfigurationProvider configurationProvider,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    #endregion

    #region Datatable Server-Side
    Task<DatatableResponseServerSide<TEntity>> DatatableServerSideAsync(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<DatatableResponseServerSide<TResult>> DatatableServerSideAsync<TResult>(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<DatatableResponseServerSide<TResult>> DatatableServerSideAsync<TResult>(
        DynamicDatatableRequest datatableRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    #endregion

    #region Datatable Client-Side
    Task<DatatableResponseClientSide<TEntity>> DatatableClientSideAsync(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<DatatableResponseClientSide<TResult>> DatatableClientSideAsync<TResult>(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<DatatableResponseClientSide<TResult>> DatatableClientSideAsync<TResult>(
        DynamicDatatableRequest datatableRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    #endregion

    #region Pagination
    Task<PaginationResponse<TEntity>> PaginationAsync(
        DynamicPaginationRequest paginationRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<PaginationResponse<TResult>> PaginationAsync<TResult>(
        DynamicPaginationRequest paginationRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    Task<PaginationResponse<TResult>> PaginationAsync<TResult>(
        DynamicPaginationRequest paginationRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default
    );
    #endregion

    #region SaveChanges
    Task SaveChangesAsync();
    #endregion
}
