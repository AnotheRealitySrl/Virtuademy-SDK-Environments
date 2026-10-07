using Virtuademy.SDK.Core.ApplicationManagement;
using Unity.VisualScripting;

using Virtuademy.ScriptingApi;

using Virtuademy.Environments.ScriptingApi;

namespace Virtuademy.SDK.Environments.VisualScripting
{
  [UnitTitle("Virtuademy Platform: Switch")]
  [UnitSurtitle("Platform")]
  [UnitShortTitle("Switch")]
  [UnitCategory("Virtuademy\\Flow")]
  public class CheckPlatformUnit : Unit
  {
    [DoNotSerialize]
    [PortLabelHidden]
    public ControlInput InputTrigger { get; private set; }
    [DoNotSerialize]
    [PortLabel("VR")]
    public ControlOutput OutputTriggerVR { get; private set; }
    [DoNotSerialize]
    [PortLabel("WebGL")]
    public ControlOutput OutputTriggerWebGL { get; private set; }
    [DoNotSerialize]
    [PortLabel("Mobile")]
    public ControlOutput OutputTriggerMobile { get; private set; }


    protected override void Definition()
    {
      InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
      {
        // Three questions rather than a switch on the flags enum: the grouped surface answers
        // "is this VR" and not "which platform is this", so that the platform list can grow
        // without every authored graph having to know the new member.
        if (IVirtuademyGameplay.Current.Platform.IsVR)
        {
          return OutputTriggerVR;
        }

        if (IVirtuademyGameplay.Current.Platform.IsWebGL)
        {
          return OutputTriggerWebGL;
        }

        if (IVirtuademyGameplay.Current.Platform.IsMobile)
        {
          return OutputTriggerMobile;
        }
        // Fallback when the platform system has not resolved a platform: mirror
        // PlatformSystem.Init, which reads the build profile's VIRTUADEMY_* scripting
        // defines. The previous fallback keyed off UNITY_ANDROID and returned the VR
        // branch, but that define is also the mobile player's, so a mobile build
        // reaching this point would take VR decisions.
#if VIRTUADEMY_VR
        return OutputTriggerVR;
#elif VIRTUADEMY_MOBILE
        return OutputTriggerMobile;
#else
        // ESupportedPlatform has no Desktop entry: the browser build is WebGL.
        return OutputTriggerWebGL;
#endif
      });

      OutputTriggerVR = ControlOutput(nameof(OutputTriggerVR));
      OutputTriggerWebGL = ControlOutput(nameof(OutputTriggerWebGL));
      OutputTriggerMobile = ControlOutput(nameof(OutputTriggerMobile));

      Succession(InputTrigger, OutputTriggerVR);
      Succession(InputTrigger, OutputTriggerWebGL);
      Succession(InputTrigger, OutputTriggerMobile);
    }
  }

  //Better way to implement this, we should deprecate the old node and find a way to fix existing graphs before updateing
  //[UnitTitle("Virtuademy Platform: Switch")]
  //[UnitSurtitle("Platform")]
  //[UnitShortTitle("Switch")]
  //[UnitCategory("Virtuademy\\Flow")]
  //public class CheckPlatformUnit : Unit
  //{
  //    [DoNotSerialize]
  //    [PortLabelHidden]
  //    public ControlInput InputTrigger { get; private set; }

  //    [DoNotSerialize]
  //    public List<ControlOutput> Outputs { get; private set; }

  //    private Dictionary<ESupportedPlatform, ControlOutput> supportedPlatformsOutputs = new Dictionary<ESupportedPlatform, ControlOutput>();

  //    protected override void Definition()
  //    {

  //        InputTrigger = ControlInput(nameof(InputTrigger), (f) =>
  //        {
  //            return supportedPlatformsOutputs[VirtuademyFramework.Current.RuntimePlatform];
  //        });

  //        Outputs = new List<ControlOutput>();

  //        foreach (ESupportedPlatform supportedPlatform in Enum.GetValues(typeof(ESupportedPlatform)))
  //        {
  //            ControlOutput output = ControlOutput(supportedPlatform.ToString());
  //            supportedPlatformsOutputs[supportedPlatform] = output;
  //            Succession(InputTrigger, output);
  //            Outputs.Add(output);
  //        }
  //    }
  //}
}
