using System.Collections.Generic;
using System.Linq;

using Unity.VisualScripting;

using Virtuademy.Environments.ScriptingApi.Placeholders;

namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
    public abstract class SyncedVariablesEventNodesWidget<T, U> : UnitWidget<T> where T : SyncedVariableBaseEventUnit<U>
    {
        private SyncedVariables syncedVariables;

        public SyncedVariables SyncedVariables
        {
            get
            {
                if (syncedVariables == null || syncedVariables.gameObject != reference.gameObject)
                {
                    var syncedVariables = reference.gameObject.GetComponentInChildren<SyncedVariables>(true);
                    if (syncedVariables != null && syncedVariables.gameObject == reference.gameObject)
                    {
                        this.syncedVariables = syncedVariables;
                    }
                }
                return syncedVariables;
            }
        }

        protected override NodeColorMix baseColor => new NodeColorMix(NodeColor.Green);
        protected VariableNameInspector nameInspector;

        protected SyncedVariablesEventNodesWidget(FlowCanvas canvas, T unit) : base(canvas, unit)
        {
        }

        public override Inspector GetPortInspector(IUnitPort port, Metadata metadata)
        {
            if (port == unit.VariableName)
            {
                // This feels so hacky. The real holy grail here would be to support attribute decorators like Unity does.
                InspectorProvider.instance.Renew(ref nameInspector, metadata, (metadata) => new VariableNameInspector(metadata, GetNameSuggestions));

                return nameInspector;
            }

            return base.GetPortInspector(port, metadata);
        }

        protected IEnumerable<string> GetNameSuggestions()
        {
            if (SyncedVariables != null)
            {
                return SyncedVariables.variableSettings.Select(x => x.name);
            }
            else
            {
                return new List<string>();
            }
        }

    }

    [Widget(typeof(SyncedVariablesEventNodes))]
    public class SyncedVariablesEventNodesWidget : SyncedVariablesEventNodesWidget<SyncedVariablesEventNodes, (string, object)>
    {
        public SyncedVariablesEventNodesWidget(FlowCanvas canvas, SyncedVariablesEventNodes unit) : base(canvas, unit)
        {

        }
    }

    [Widget(typeof(OnSyncedVariableInit))]
    public class OnSyncedVariableInitWidget : SyncedVariablesEventNodesWidget<OnSyncedVariableInit, (string, object, bool)>
    {
        public OnSyncedVariableInitWidget(FlowCanvas canvas, OnSyncedVariableInit unit) : base(canvas, unit)
        {
        }
    }

    [Widget(typeof(OnSyncedVariableInitEventUnit))]
    public class OnSyncedVariableInitEventUnitWidget : SyncedVariablesEventNodesWidget<OnSyncedVariableInitEventUnit, (string, object)>
    {
        public OnSyncedVariableInitEventUnitWidget(FlowCanvas canvas, OnSyncedVariableInitEventUnit unit) : base(canvas, unit)
        {
        }
    }
}
