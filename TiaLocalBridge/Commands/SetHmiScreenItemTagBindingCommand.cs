using System;
using System.Reflection;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class SetHmiScreenItemTagBindingCommand : ITiaCommand
    {
        public string Name => "SETHMISCREENITEMTAGBINDING";
        public string Description => "Sets a Unified HMI screen item tag binding. For ProcessValue this first tries the direct ProcessValue property; if the target control rejects that path, it falls back to a TagDynamization using the specified HMI tag name.";
        public string Usage => "SETHMISCREENITEMTAGBINDING|<device-reference>|<screen-reference>|<item-name>|<target-property>|<tag-name>";
        public string Example => "SETHMISCREENITEMTAGBINDING|DemoProject/HMI_1|Config/Overview|Io_Speed|ProcessValue|HmiSpeed";
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
            var property = item.GetType().GetProperty(targetProperty, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            string directBindingFailure = null;

            if (string.Equals(targetProperty, "ProcessValue", StringComparison.OrdinalIgnoreCase) && property != null && property.CanWrite && property.PropertyType == typeof(string))
            {
                try
                {
                    var previousValue = property.CanRead ? property.GetValue(item, null) as string : null;
                    property.SetValue(item, resolvedTagName, null);
                    var updatedValue = property.GetValue(item, null) as string;
                    return $"Updated direct tag binding 'ProcessValue' on Unified HMI screen item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}' [OldValue={previousValue ?? "<empty>"}, NewValue={updatedValue ?? "<empty>"}].";
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

                var fallbackSuffix = string.IsNullOrWhiteSpace(directBindingFailure)
                    ? string.Empty
                    : $" Direct ProcessValue binding failed first and the command fell back to TagDynamization. Direct error: {directBindingFailure}";

                return $"Updated tag dynamization binding '{tagDynamization.PropertyName}' on Unified HMI screen item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}' [OldTag={previousTag ?? "<empty>"}, NewTag={tagDynamization.Tag ?? "<empty>"}].{fallbackSuffix}";
            }
            catch (TargetInvocationException ex)
            {
                var fallbackFailure = ex.InnerException?.Message ?? ex.Message;
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(directBindingFailure)
                    ? $"Failed to bind tag '{resolvedTagName}' to property '{targetProperty}' on item '{item.Name}'. Details: {fallbackFailure}"
                    : $"Failed to bind tag '{resolvedTagName}' to property '{targetProperty}' on item '{item.Name}'. Direct ProcessValue binding error: {directBindingFailure}. TagDynamization fallback error: {fallbackFailure}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(directBindingFailure)
                    ? $"Failed to bind tag '{resolvedTagName}' to property '{targetProperty}' on item '{item.Name}'. Details: {ex.Message}"
                    : $"Failed to bind tag '{resolvedTagName}' to property '{targetProperty}' on item '{item.Name}'. Direct ProcessValue binding error: {directBindingFailure}. TagDynamization fallback error: {ex.Message}");
            }
        }
    }
}
