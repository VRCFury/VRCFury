using System;
using System.Collections;
using System.Reflection;

public class DefaultsCleaner {
    public static bool Cleanup(object obj) {
        if (obj == null) return false;
        Type objType = obj.GetType();
        if (!objType.Name.Contains("Senky")) return false;
        FieldInfo[] fields = objType.GetFields();
        foreach (FieldInfo field in fields) {
            object value = field.GetValue(obj);
            if (value is IList) {
                var list = value as IList;
                for (var i = 0; i < list.Count; i++) {
                    bool remove = Cleanup(list[i]);
                    if (remove) {
                        var elemType = list[i].GetType();
                        var newInst = Activator.CreateInstance(elemType);
                        list.RemoveAt(i);
                        list.Insert(i, newInst);
                    }
                }
            } else {
                if (field.Name == "ResetMePlease") {
                    if ((bool)value) {
                        return true;
                    }
                } else {
                    var type = field.FieldType;
                    if (type.IsClass) {
                        Cleanup(value);
                    }
                }
            }
        }
        return false;
    }
}
