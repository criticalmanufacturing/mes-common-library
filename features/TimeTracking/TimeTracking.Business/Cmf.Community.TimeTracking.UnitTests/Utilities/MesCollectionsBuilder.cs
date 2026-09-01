using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Repository.Abstractions;
using Moq;

namespace Cmf.Community.TimeTracking.UnitTests.Utilities
{
    internal class MesCollectionsBuilder
    {
        public Mock<IUtilities> Utilities { get; private set; }
        public Mock<IEntityFactory> EntityFactory { get; private set; }
        public Mock<ICallContext> CallContext { get; private set; }
        public Mock<IEntityRelationRepository<IEntityRelation>> EntityRelationRepository { get; private set; }

        public MesCollectionsBuilder(Mock<IUtilities>? utilities = null,
            Mock<IEntityFactory>? entityFactory = null,
            Mock<ICallContext>? callContext = null)
        {
            Utilities = utilities ?? new Mock<IUtilities>();
            EntityFactory = entityFactory ?? new Mock<IEntityFactory>();
            CallContext = callContext ?? new Mock<ICallContext>();
            EntityRelationRepository = new Mock<IEntityRelationRepository<IEntityRelation>>();
        }

        public EntityRelationCollection<IEntityRelation> NewEntityRelationCollection(params IEntityRelation[] items)
        {
            var collection = new EntityRelationCollection<IEntityRelation>(
                new EntityRelationDependencies(
                    new EntityBaseDependencies(new Foundation.Common.Base.CoreBaseDependencies(Utilities.Object), CallContext.Object, EntityFactory.Object))
                , EntityRelationRepository.Object);
            collection.AddRange(items);
            return collection;
        }
    }
}