using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;
public static class EntityFactoryExtensions
{
    /// <summary>
    /// Create an <typeparamref name="T"/> entity with the given name.
    /// </summary>
    /// <typeparam name="T"><see cref="IEntityBase"/> type to create the entity.</typeparam>
    /// <param name="factory"><see cref="IEntityFactory"/> instance.</param>
    /// <param name="name">Name of the instance to be created.</param>
    /// <param name="load">Whether to load the instance. Make sure that the instance exist before marking it.</param>
    /// <returns>Instance of the<typeparamref name="T"/> created by the <see cref="IEntityFactory"/>.</returns>
    public static T CreateWithName<T>(this IEntityFactory factory, string name, bool load = false) where T : IEntityBase
    {
        T entity = factory.Create<T>();
        entity.Name = name;

        if (load)
        {
            entity.Load();
        }

        return entity;
    }

    /// <summary>
    /// Create a collection of entities type of <typeparamref name="T"/> with the given names.
    /// </summary>
    /// <typeparam name="T"><see cref="IEntityBaseCollection{K}"/> type to create the collection of entities.</typeparam>
    /// <typeparam name="K"><see cref="IEntityBase"/> type to create each of the entities.</typeparam>
    /// <param name="factory"><see cref="IEntityFactory"/> instance.</param>
    /// <param name="names">Names of each instance to be created.</param>
    /// <param name="load">Whether to load the instance. Make sure that the instance exist before marking it.</param>
    /// <returns>Instances of the <typeparamref name="T"/> created by the <see cref="IEntityFactory"/>.</returns>
    public static T CreateCollectionWithNames<T, K>(this IEntityFactory factory, IEnumerable<string> names, bool load = false) where T : IEntityBaseCollection<K> where K : IEntityBase
    {
        T collection = factory.CreateCollection<T>();
        foreach (string name in names)
        {
            collection.Add(factory.CreateWithName<K>(name, load: false));
        }

        if (load)
        {
            collection.Load();
        }

        return collection;
    }
}
