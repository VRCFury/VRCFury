using JetBrains.Annotations;
using VF.Model.StateAction;

namespace com.vrcfury.api.Actions {
    [PublicAPI]
    public class FuryFlipbookBuilder {
        private readonly FlipBookBuilderAction f;

        internal FuryFlipbookBuilder(FlipBookBuilderAction f) {
            this.f = f;
        }

        public Page AddPage() {
            var p = new FlipBookBuilderAction.FlipBookPage();
            f.pages.Add(p);
            return new Page(p);
        }

        [PublicAPI]
        public class Page {
            private readonly FlipBookBuilderAction.FlipBookPage p;

            internal Page(FlipBookBuilderAction.FlipBookPage p) {
                this.p = p;
            }

            public FuryActionSet GetActions() {
                return new FuryActionSet(p.state);
            }
        }
    }
}
