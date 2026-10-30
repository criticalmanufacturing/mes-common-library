using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

public interface IResourceHelper
{
    public void AttachConsumablesToResource(IResource instance, IMaterialCollection materials, int order = 0);
}