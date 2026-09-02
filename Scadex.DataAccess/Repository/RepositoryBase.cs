using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Scadex.Core.BaseRequestModels;
using Scadex.Core.Model;
using Scadex.Core.Utils.Datatable;
using Scadex.Core.Utils.DynamicQuery;
using Scadex.Core.Utils.Pagination;
using Scadex.Model.Entities;
using System.Linq.Expressions;

namespace Scadex.DataAccess.Repository;

public class RepositoryBase<TEntity, TContext> : IRepository<TEntity>, IRepositoryAsync<TEntity>
    where TEntity : class, IEntity
    where TContext : IdentityDbContext<User, Role, Guid>
    // DbContext 
    // IdentityDbContext<User, IdentityRole<Guid>, Guid> && IdentityDbContext<User, Role<Guid>, Guid>
{
    protected readonly TContext _context;
    public RepositoryBase(TContext context) => _context = context;


    // ############################# Sync Methods #############################
    #region Add
    public TEntity Add(TEntity entity)
    {
        _context.Set<TEntity>().Add(entity);
        return entity;
    }

    public ICollection<TEntity> Add(IEnumerable<TEntity> entities)
    {
        _context.Set<TEntity>().AddRange(entities);
        return entities.ToList();
    }

    public TEntity AddAndSave(TEntity entity)
    {
        _context.Set<TEntity>().Add(entity);
        _context.SaveChanges();
        return entity;
    }

    public ICollection<TEntity> AddAndSave(IEnumerable<TEntity> entities)
    {
        _context.Set<TEntity>().AddRange(entities);
        _context.SaveChanges();
        return entities.ToList();
    }
    #endregion

    #region Update
    public TEntity UpdateAndSave(TEntity entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        _context.SaveChanges();
        return entity;
    }

    public ICollection<TEntity> UpdateAndSave(IEnumerable<TEntity> entities)
    {
        _context.Set<TEntity>().UpdateRange(entities);
        _context.SaveChanges();
        return entities.ToList();
    }
    #endregion

    #region Delete
    public void Delete(TEntity entity)
    {
        _context.Set<TEntity>().Remove(entity);
    }

    public void Delete(IEnumerable<TEntity> entities)
    {
        _context.Set<TEntity>().RemoveRange(entities);
    }

    public int Delete(Expression<Func<TEntity, bool>> where)
    {
        var entitiesToDelete = _context.Set<TEntity>().Where(where).ToList();
        if (entitiesToDelete.Count == 0) return 0;

        _context.Set<TEntity>().RemoveRange(entitiesToDelete);
        return entitiesToDelete.Count;
    }

    public void DeleteAndSave(TEntity entity)
    {
        _context.Set<TEntity>().Remove(entity);
        _context.SaveChanges();
    }

    public void DeleteAndSave(IEnumerable<TEntity> entities)
    {
        _context.Set<TEntity>().RemoveRange(entities);
        _context.SaveChanges();
    }

    public int DeleteAndSave(Expression<Func<TEntity, bool>> where)
    {
        var entitiesToDelete = _context.Set<TEntity>().Where(where).ToList();
        if (entitiesToDelete.Count == 0) return 0;

        _context.Set<TEntity>().RemoveRange(entitiesToDelete);
        _context.SaveChanges();
        return entitiesToDelete.Count;
    }
    #endregion

    #region Restore
    public int Restore(Expression<Func<TEntity, bool>> where)
    {
        var entities = _context.Set<TEntity>().IgnoreQueryFilters().Where(where).ToList();
        int restored = 0;
        foreach (var entity in entities)
        {
            if (entity is ISoftDeletableEntity softDeletableEntity)
            {
                softDeletableEntity.IsDeleted = false;
                softDeletableEntity.DeletedBy = null;
                softDeletableEntity.DeletedDateUtc = null;
                restored++;
            }
        }
        return restored;
    }

    public int RestoreAndSave(Expression<Func<TEntity, bool>> where)
    {
        var entities = _context.Set<TEntity>().IgnoreQueryFilters().Where(where).ToList();
        int restored = 0;
        foreach (var entity in entities)
        {
            if (entity is ISoftDeletableEntity softDeletableEntity)
            {
                softDeletableEntity.IsDeleted = false;
                softDeletableEntity.DeletedBy = null;
                softDeletableEntity.DeletedDateUtc = null;
                restored++;
            }
        }

        if (restored == 0) return 0;

        _context.SaveChanges();
        return restored;
    }
    #endregion

    #region IsExist & Count
    public bool IsExist(Filter? filter = null, Expression<Func<TEntity, bool>>? where = null, bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);

        return query.Any();
    }

    public int Count(Filter? filter = null, Expression<Func<TEntity, bool>>? where = null, bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);

        return query.Count();
    }
    #endregion

    #region Get
    public TEntity? Get(
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        bool tracking = true)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);
        if (!tracking) query = query.AsNoTracking();

        return query.FirstOrDefault();
    }

    public TResult? Get<TResult>(
        Expression<Func<TEntity, TResult>> select,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);

        return query.Select(select).FirstOrDefault();
    }

    public TResult? Get<TResult>(
        IConfigurationProvider configurationProvider,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);

        return query.ProjectTo<TResult>(configurationProvider).FirstOrDefault();
    }
    #endregion

    #region GetAll
    public ICollection<TEntity>? GetAll(
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        bool tracking = true)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);
        if (!tracking) query = query.AsNoTracking();

        return query.ToList();
    }

    public ICollection<TResult>? GetAll<TResult>(
        Expression<Func<TEntity, TResult>> select,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);

        return query.Select(select).ToList();
    }

    public ICollection<TResult>? GetAll<TResult>(
        IConfigurationProvider configurationProvider,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);

        return query.ProjectTo<TResult>(configurationProvider).ToList();
    }
    #endregion

    #region Datatable Server-Side
    public DatatableResponseServerSide<TEntity> DatatableServerSide(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return query.ToDatatableServerSide(datatableRequest);
    }

    public DatatableResponseServerSide<TResult> DatatableServerSide<TResult>(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return query.Select(select).ToDatatableServerSide(datatableRequest);
    }

    public DatatableResponseServerSide<TResult> DatatableServerSide<TResult>(
        DynamicDatatableRequest datatableRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return query.ProjectTo<TResult>(configurationProvider).ToDatatableServerSide(datatableRequest);
    }
    #endregion

    #region Datatable Client-Side
    public DatatableResponseClientSide<TEntity> DatatableClientSide(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return query.ToDatatableClientSide();
    }

    public DatatableResponseClientSide<TResult> DatatableClientSide<TResult>(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return query.Select(select).ToDatatableClientSide();
    }

    public DatatableResponseClientSide<TResult> DatatableClientSide<TResult>(
        DynamicDatatableRequest datatableRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return query.ProjectTo<TResult>(configurationProvider).ToDatatableClientSide();
    }
    #endregion

    #region Pagination
    public PaginationResponse<TEntity> Pagination(
        DynamicPaginationRequest paginationRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (paginationRequest.Filter != null) query = query.ToFilter(paginationRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (paginationRequest.Sorts != null) query = query.ToSort(paginationRequest.Sorts);
        if (include != null) query = include(query);

        return query.ToPaginate(paginationRequest);
    }

    public PaginationResponse<TResult> Pagination<TResult>(
        DynamicPaginationRequest paginationRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (paginationRequest.Filter != null) query = query.ToFilter(paginationRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (paginationRequest.Sorts != null) query = query.ToSort(paginationRequest.Sorts);
        if (include != null) query = include(query);

        return query.Select(select).ToPaginate(paginationRequest);
    }

    public PaginationResponse<TResult> Pagination<TResult>(
        DynamicPaginationRequest paginationRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (paginationRequest.Filter != null) query = query.ToFilter(paginationRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (paginationRequest.Sorts != null) query = query.ToSort(paginationRequest.Sorts);
        if (include != null) query = include(query);

        return query.ProjectTo<TResult>(configurationProvider).ToPaginate(paginationRequest);
    }
    #endregion

    #region SaveChanges
    public void SaveChanges()
    {
        _context.SaveChanges();
    }
    #endregion

    // ############################# Async Methods #############################
    #region Add
    public async Task<TEntity> AddAndSaveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _context.Set<TEntity>().Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ICollection<TEntity>> AddAndSaveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        _context.Set<TEntity>().AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
        return entities.ToList();
    }
    #endregion

    #region Update
    public async Task<TEntity> UpdateAndSaveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _context.Entry(entity).State = EntityState.Modified;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ICollection<TEntity>> UpdateAndSaveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        _context.Set<TEntity>().UpdateRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
        return entities.ToList();
    }
    #endregion

    #region Delete
    public async Task DeleteAndSaveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _context.Set<TEntity>().Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAndSaveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        _context.Set<TEntity>().RemoveRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteAndSaveAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default)
    {
        var entitiesToDelete = await _context.Set<TEntity>().Where(where).ToListAsync(cancellationToken);
        if (entitiesToDelete.Count == 0) return 0;

        _context.Set<TEntity>().RemoveRange(entitiesToDelete);
        await _context.SaveChangesAsync(cancellationToken);
        return entitiesToDelete.Count;
    }
    #endregion

    #region Restore
    public async Task<int> RestoreAndSaveAsync(Expression<Func<TEntity, bool>> where, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<TEntity>().IgnoreQueryFilters().Where(where).ToListAsync(cancellationToken);
        int restored = 0;
        foreach (var entity in entities)
        {
            if (entity is ISoftDeletableEntity softDeletableEntity)
            {
                softDeletableEntity.IsDeleted = false;
                softDeletableEntity.DeletedBy = null;
                softDeletableEntity.DeletedDateUtc = null;
                restored++;
            }
        }

        if (restored == 0) return 0;

        await _context.SaveChangesAsync(cancellationToken);
        return restored;
    }
    #endregion

    #region IsExist & Count
    public async Task<bool> IsExistAsync(
        Filter? filter = null,
        Expression<Func<TEntity, bool>>? where = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        Filter? filter = null,
        Expression<Func<TEntity, bool>>? where = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);

        return await query.CountAsync(cancellationToken);
    }
    #endregion

    #region Get
    public async Task<TEntity?> GetAsync(
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        bool tracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);
        if (!tracking) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TResult?> GetAsync<TResult>(
        Expression<Func<TEntity, TResult>> select,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);

        return await query.Select(select).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TResult?> GetAsync<TResult>(
        IConfigurationProvider configurationProvider,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);

        return await query.ProjectTo<TResult>(configurationProvider).FirstOrDefaultAsync(cancellationToken);
    }
    #endregion

    #region GetAll
    public async Task<ICollection<TEntity>?> GetAllAsync(
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        bool tracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);
        if (!tracking) query = query.AsNoTracking();

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<ICollection<TResult>?> GetAllAsync<TResult>(
        Expression<Func<TEntity, TResult>> select,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);

        return await query.Select(select).ToListAsync(cancellationToken);
    }

    public async Task<ICollection<TResult>?> GetAllAsync<TResult>(
        IConfigurationProvider configurationProvider,
        Filter? filter = null,
        IEnumerable<Sort>? sorts = null,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (filter != null) query = query.ToFilter(filter);
        if (orderBy != null) query = orderBy(query);
        if (sorts != null) query = query.ToSort(sorts);
        if (include != null) query = include(query);

        return await query.ProjectTo<TResult>(configurationProvider).ToListAsync(cancellationToken);
    }
    #endregion

    #region Datatable Server-Side
    public async Task<DatatableResponseServerSide<TEntity>> DatatableServerSideAsync(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return await query.ToDatatableServerSideAsync(datatableRequest, cancellationToken);
    }

    public async Task<DatatableResponseServerSide<TResult>> DatatableServerSideAsync<TResult>(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return await query.Select(select).ToDatatableServerSideAsync(datatableRequest, cancellationToken);
    }

    public async Task<DatatableResponseServerSide<TResult>> DatatableServerSideAsync<TResult>(
        DynamicDatatableRequest datatableRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return await query.ProjectTo<TResult>(configurationProvider).ToDatatableServerSideAsync(datatableRequest, cancellationToken);
    }
    #endregion

    #region Datatable Client-Side
    public async Task<DatatableResponseClientSide<TEntity>> DatatableClientSideAsync(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return await query.ToDatatableClientSideAsync(cancellationToken);
    }

    public async Task<DatatableResponseClientSide<TResult>> DatatableClientSideAsync<TResult>(
        DynamicDatatableRequest datatableRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return await query.Select(select).ToDatatableClientSideAsync(cancellationToken);
    }

    public async Task<DatatableResponseClientSide<TResult>> DatatableClientSideAsync<TResult>(
        DynamicDatatableRequest datatableRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (datatableRequest.Filter != null) query = query.ToFilter(datatableRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (datatableRequest.Sorts != null)
        {
            query = query.ToSort(datatableRequest.Sorts);
            datatableRequest.Order = null;
        }
        if (include != null) query = include(query);

        return await query.ProjectTo<TResult>(configurationProvider).ToDatatableClientSideAsync(cancellationToken);
    }
    #endregion

    #region Pagination
    public async Task<PaginationResponse<TEntity>> PaginationAsync(
        DynamicPaginationRequest paginationRequest,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (paginationRequest.Filter != null) query = query.ToFilter(paginationRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (paginationRequest.Sorts != null) query = query.ToSort(paginationRequest.Sorts);
        if (include != null) query = include(query);

        return await query.ToPaginateAsync(paginationRequest, cancellationToken);
    }

    public async Task<PaginationResponse<TResult>> PaginationAsync<TResult>(
        DynamicPaginationRequest paginationRequest,
        Expression<Func<TEntity, TResult>> select,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (paginationRequest.Filter != null) query = query.ToFilter(paginationRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (paginationRequest.Sorts != null) query = query.ToSort(paginationRequest.Sorts);
        if (include != null) query = include(query);

        return await query.Select(select).ToPaginateAsync(paginationRequest, cancellationToken);
    }

    public async Task<PaginationResponse<TResult>> PaginationAsync<TResult>(
        DynamicPaginationRequest paginationRequest,
        IConfigurationProvider configurationProvider,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object?>>? include = null,
        bool ignoreFilters = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TEntity>().AsQueryable().AsNoTracking();

        if (ignoreFilters) query = query.IgnoreQueryFilters();
        if (where != null) query = query.Where(where);
        if (paginationRequest.Filter != null) query = query.ToFilter(paginationRequest.Filter);
        if (orderBy != null) query = orderBy(query);
        if (paginationRequest.Sorts != null) query = query.ToSort(paginationRequest.Sorts);
        if (include != null) query = include(query);

        return await query.ProjectTo<TResult>(configurationProvider).ToPaginateAsync(paginationRequest, cancellationToken);
    }
    #endregion

    #region SaveChanges
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
    #endregion
}
