using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class SetHwPropertyCommand : ITiaCommand
    {
        public string Name => "SETHWPROPERTY";
        public string Description => "Sets a writable hardware property on a device or device item. Use GETHWPROPERTIES first to discover the exact property name and supported value type.";
        public string Usage => "SETHWPROPERTY|<device-reference>|<target-reference>|<property-name>|<value>";
        public string Example => "SETHWPROPERTY|DemoProject/PLC_1|DEVICE|Name|PLC_1_Renamed";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<target-reference>", "<property-name>", "<value>");
            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var targetResolution = CommandSupport.ResolveHardwareObject(deviceResolution.Device, providedArgs[1]);
            var propertyName = providedArgs[2];
            var rawValue = providedArgs[3];
            var attributeInfo = CommandSupport.ResolveAttributeInfo(targetResolution.EngineeringObject, propertyName);

            if (attributeInfo.AccessMode != EngineeringAttributeAccessMode.Write && attributeInfo.AccessMode != EngineeringAttributeAccessMode.ReadWrite)
            {
                throw new InvalidOperationException($"Hardware property '{attributeInfo.Name}' is not writable on {targetResolution.TargetKind} '{targetResolution.TargetReference}'. Access mode is {attributeInfo.AccessMode}.");
            }

            var previousValue = CommandSupport.TryGetAttributeValue(targetResolution.EngineeringObject, attributeInfo.Name, out string previousReadError);
            var convertedValue = CommandSupport.ConvertTextToAttributeValue(targetResolution.EngineeringObject, attributeInfo, rawValue);

            try
            {
                targetResolution.EngineeringObject.SetAttribute(attributeInfo.Name, convertedValue);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set hardware property '{attributeInfo.Name}' on {targetResolution.TargetKind} '{targetResolution.TargetReference}'. Details: {ex.Message}");
            }

            var updatedValue = CommandSupport.TryGetAttributeValue(targetResolution.EngineeringObject, attributeInfo.Name, out string updatedReadError);
            var previousValueText = string.IsNullOrWhiteSpace(previousReadError)
                ? CommandSupport.FormatEngineeringValue(previousValue)
                : $"<read-error:{previousReadError}>";
            var updatedValueText = string.IsNullOrWhiteSpace(updatedReadError)
                ? CommandSupport.FormatEngineeringValue(updatedValue)
                : $"<read-error:{updatedReadError}>";

            return $"Updated hardware property '{attributeInfo.Name}' on {targetResolution.TargetKind} '{targetResolution.TargetReference}' for device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}' [OldValue={previousValueText}, NewValue={updatedValueText}, ConvertedType={convertedValue?.GetType().Name ?? "<null>"}].";
        }
    }
}
