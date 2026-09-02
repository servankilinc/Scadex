using AutoMapper;
using Microsoft.EntityFrameworkCore.Query;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Model;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.DynamicQuery;
using Scadex.Core.Utils.Pagination;
using System.Linq.Expressions;

namespace Scadex.DataAccess.Repository;

public interface IRepository<TEntity> where TEntity : IEntity
{
    #region Add
    TEntity Add(TEntity entity);
    TEntity AddAndSave(TEntity entity);
    ICollection<TEntity> Add(IEnumerable<TEntity> entities);
    ICollection<TEntity> AddAndSave(IEnumerable<TEntity> entities);
    #endregion

    #region Update
    TEntity UpdateAndSave(TEntity entity);
    ICollection<TEntity> UpdateAndSave(IEnumerable<TEntity> entities);
    #endregion

    #region Delete
    void Delete(TEntity entity);
    void DeleteAndSave(TEntity entity);
    void Delete(IEnumerable<TEntity> entities);
    void DeleteAndSave(IEnumerable<TEntity> entities);
    int Delete(Expression<Func<TEntity, bool>> where);
    int DeleteAndSave(Expression<Func<TEntity, bool>> where);
    #endregion

    #region Restore
    int Restore(Expression<Func<TEntity, bool>> where);
    int RestoreAndSave(Expression<Func<TEntity, bool>> where);
    #endregion

    #region IsExist & Count
    bool IsExist(Filter? filter = null, Expression<Func<TEntity, bool>>? where = null, bool ignoreFilters = false);
    int Count(Filter? filter = null, Expression<Func<TEntity, bool>>? where = null, bool ignoreFilters = false);
    #endregion

    #region Get
    TEntity? Get(
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        bool tracking = true
    );
    TResult? Get<TResult>(
        Expression<Func<TEntity, TResult>> select,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    TResult? Get<TResult>(
        IConfigurationProvider configurationProvider,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    #endregion

    #region GetAll
    ICollection<TEntity>? GetAll(
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        bool tracking = true
    );
    ICollection<TResult>? GetAll<TResult>(
        Expression<Func<TEntity, TResult>> select,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    ICollection<TResult>? GetAll<TResult>(
        IConfigurationProvider configurationProvider,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    #endregion

    #region Datatable Server-Side
    DatatableResponseServerSide<TEntity> DatatableServerSide(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    DatatableResponseServerSide<TResult> DatatableServerSide<TResult>(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    DatatableResponseServerSide<TResult> DatatableServerSide<TResult>(
        DynamicDatatableRequest datatableRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    #endregion

    #region Datatable Client-Side
    DatatableResponseClientSide<TEntity> DatatableClientSide(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    DatatableResponseClientSide<TResult> DatatableClientSide<TResult>(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    DatatableResponseClientSide<TResult> DatatableClientSide<TResult>(
        DynamicDatatableRequest datatableRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    #endregion

    #region Pagination
    PaginationResponse<TEntity> Pagination(
        DynamicPaginationRequest paginationRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    PaginationResponse<TResult> Pagination<TResult>(
        DynamicPaginationRequest paginationRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    PaginationResponse<TResult> Pagination<TResult>(
        DynamicPaginationRequest paginationRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false
    );
    #endregion

    #region SaveChanges
    void SaveChanges();
    #endregion
}
