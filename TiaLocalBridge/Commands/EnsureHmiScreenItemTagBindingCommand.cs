using System;
using System.Linq;
using System.Reflection;
using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.Dynamization;

namespace TiaLocalBridge.Commands
{
    internal class EnsureHmiScreenItemTagBindingCommand : ITiaCommand
    {
        public string Name => "ENSUREHMISCREENITEMTAGBINDING";
        public string Description => "Creates or updates a Unified HMI screen item tag binding and returns 'unchanged' when the requested binding already exists. For ProcessValue this first tries the direct property path and falls back to TagDynamization when the target control requires it.";
        public string Usage => "ENSUREHMISCREENITEMTAGBINDING|<device-reference>|<screen-reference>|<item-name>|<target-property>|<tag-name>";
        public string Example => "ENSUREHMISCREENITEMTAGBINDING|DemoProject/HMI_1|Config/Overview|Io_Speed|ProcessValue|HmiSpeed";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<screen-reference>", "<item-name>", "<target-property>", "<tag-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var screen = CommandSupport.ResolveUnifiedHmiScreen(hmiSoftware, providedArgs[1]);
            var item = CommandSupport.ResolveUnifiedHmiScreenItem(screen.Screen, providedArgs[2]);
            var targetProperty = providedArgs[3].Trim();
            var tagName = providedArgs[4].Trim();

            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("HMI tag name cannot be empty.");
            }

            var tagResolution = CommandSupport.ResolveUnifiedHmiTagByName(hmiSoftware, tagName);
            var resolvedTagName = tagResolution.Tag.Name;
            var existingTagDynamization = CommandSupport.GetUnifiedHmiDynamizations(item)
                .OfType<TagDynamization>()
                .FirstOrDefault(binding => string.Equals(binding.PropertyName, targetProperty, StringComparison.OrdinalIgnoreCase));

            if (existingTagDynamization != null && string.Equals(existingTagDynamization.Tag ?? string.Empty, resolvedTagName, StringComparison.OrdinalIgnoreCase))
            {
                return $"Ensured Unified HMI screen item tag binding on item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}' [Operation=unchanged, Mode=TagDynamization, Property={existingTagDynamization.PropertyName}, Tag={existingTagDynamization.Tag ?? "<empty>"}].";
            }

            var property = item.GetType().GetProperty(targetProperty, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (existingTagDynamization == null && string.Equals(targetProperty, "ProcessValue", StringComparison.OrdinalIgnoreCase) && property != null && property.CanRead && property.PropertyType == typeof(string))
            {
                var currentValue = property.GetValue(item, null) as string;
                if (string.Equals(currentValue ?? string.Empty, resolvedTagName, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Ensured Unified HMI screen item tag binding on item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}' [Operation=unchanged, Mode=DirectProperty, Property=ProcessValue, Tag={currentValue ?? "<empty>"}].";
                }
            }

            string directBindingFailure = null;
            if (string.Equals(targetProperty, "ProcessValue", StringComparison.OrdinalIgnoreCase) && property != null && property.CanWrite && property.PropertyType == typeof(string))
            {
                try
                {
                    var previousValue = property.CanRead ? property.GetValue(item, null) as string : null;
                    property.SetValue(item, resolvedTagName, null);
                    var updatedValue = property.GetValue(item, null) as string;
                    var operation = string.Equals(previousValue ?? string.Empty, updatedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        ? "unchanged"
                        : "updated";
                    return $"Ensured Unified HMI screen item tag binding on item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}' [Operation={operation}, Mode=DirectProperty, Property=ProcessValue, OldTag={previousValue ?? "<empty>"}, NewTag={updatedValue ?? "<empty>"}].";
                }
                catch (TargetInvocationException ex)
                {
                    directBindingFailure = ex.InnerException?.Message ?? ex.Message;
                }
                catch (Exception ex)
                {
                    directBindingFailure = ex.Message;
                }
            }

            try
            {
                var tagDynamization = CommandSupport.EnsureUnifiedHmiTagDynamization(item, targetProperty);
                var previousTag = tagDynamization.Tag;
                tagDynamization.Tag = resolvedTagName;
                var operation = string.Equals(previousTag ?? string.Empty, tagDynamization.Tag ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    ? "unchanged"
                    : "updated";
                var fallbackSuffix = string.IsNullOrWhiteSpace(directBindingFailure)
                    ? string.Empty
                    : $" Direct ProcessValue binding failed first and the command fell back to TagDynamization. Direct error: {directBindingFailure}";

                return $"Ensured Unified HMI screen item tag binding on item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}' [Operation={operation}, Mode=TagDynamization, Property={tagDynamization.PropertyName}, OldTag={previousTag ?? "<empty>"}, NewTag={tagDynamization.Tag ?? "<empty>"}].{fallbackSuffix}";
            }
            catch (TargetInvocationException ex)
            {
                var fallbackFailure = ex.InnerException?.Message ?? ex.Message;
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(directBindingFailure)
                    ? $"Failed to ensure tag '{resolvedTagName}' on property '{targetProperty}' for item '{item.Name}'. Details: {fallbackFailure}"
                    : $"Failed to ensure tag '{resolvedTagName}' on property '{targetProperty}' for item '{item.Name}'. Direct ProcessValue binding error: {directBindingFailure}. TagDynamization fallback error: {fallbackFailure}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(directBindingFailure)
                    ? $"Failed to ensure tag '{resolvedTagName}' on property '{targetProperty}' for item '{item.Name}'. Details: {ex.Message}"
                    : $"Failed to ensure tag '{resolvedTagName}' on property '{targetProperty}' for item '{item.Name}'. Direct ProcessValue binding error: {directBindingFailure}. TagDynamization fallback error: {ex.Message}");
            }
        }
    }
}
