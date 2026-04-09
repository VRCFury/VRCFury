using System;
using System.Collections.Generic;

namespace VF.Model.Feature {
    /**
     * This class exists because of an annoying unity bug. If a class is serialized with no fields (an empty class),
     * then if you try to deserialize it into a class with fields, unity blows up and fails. This means we can never
     * add fields to a class which started without any. This, all features must be migrated to NewFeatureModel,
     * which contains one field by default (version).
     */
    [Serializable]
    internal abstract class LegacyFeatureModel : FeatureModel {
        public abstract NewFeatureModel CreateNewInstance();
        public override IList<FeatureModel> Migrate(MigrateRequest request) {
            return new FeatureModel[] { CreateNewInstance() };
        }
    }
}