using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.DataStructures;

public class AttachedConsumable
{
    public IResource ConsumableFeed { get; set; }
    public IMaterial Consumable { get; set; }
    public int Order { get; set; }
}
