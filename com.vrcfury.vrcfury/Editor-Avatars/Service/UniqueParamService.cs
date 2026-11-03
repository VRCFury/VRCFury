using System.Collections.Generic;
using System.Text.RegularExpressions;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    // Reserves unused names. Allocating a name does not create an animator or expression parameter.
    [VFService]
    internal class UniqueParamService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ExpressionParamsService expressionParams;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly ParameterSourceService parameterSourceService;

        private readonly HashSet<string> reserved = new HashSet<string>();

        public string GetUniqueName(string originalName) {
            originalName = Regex.Replace(originalName, @"^VF\d+_", "");
            var name = ("VF" + globals.currentFeatureNum + "_" + originalName).MakeUnique(IsUsed);
            reserved.Add(name);
            parameterSourceService.RecordParamSource(name, globals.currentFeatureObjectPath, originalName);
            return name;
        }

        private bool IsUsed(string name) {
            if (reserved.Contains(name)) return true;
            if (expressionParams.Contains(name)) return true;
            foreach (var controller in controllers.GetAllUsedControllers()) {
                if (controller.TryGetParam(name, out _)) return true;
            }
            return false;
        }
    }
}
