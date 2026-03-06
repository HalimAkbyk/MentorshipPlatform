namespace MentorshipPlatform.Application.Common.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class RequiresFeatureAttribute : Attribute
{
    public string FeatureFlagKey { get; }

    public RequiresFeatureAttribute(string featureFlagKey)
    {
        FeatureFlagKey = featureFlagKey;
    }
}
