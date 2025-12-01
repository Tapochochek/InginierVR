using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VRDebugHelper : MonoBehaviour
{
    public XRRayInteractor rayInteractor;
    public VRSketchManager mgr; // опционально - чтобы видеть текущую стадию

    void Update()
    {
        if (rayInteractor == null)
        {
            Debug.LogWarning("[VRDebug] rayInteractor == null");
            return;
        }

        // UI raycast
        rayInteractor.TryGetCurrentUIRaycastResult(out var uiHit);
        string uiInfo = uiHit.isValid ? $"UI hit: {uiHit.gameObject.name}" : "UI hit: none";

        // 3D raycast
        bool worldHit = rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit);
        string worldInfo = worldHit && hit.collider != null ? $"World hit: {hit.collider.gameObject.name} (pos {hit.point.ToString("F2")})" : "World hit: none";

        // trigger state (XR controller)
        string trig = "N/A";
        if (rayInteractor.xrController != null)
        {
            var st = rayInteractor.xrController.activateInteractionState;
            trig = $"triggerDown:{st.activatedThisFrame} pressed:{st.active} up:{st.deactivatedThisFrame}";
        }

        string stage = mgr != null ? mgr.stage.ToString() : "no-mgr";

        Debug.Log($"[VRDebug] Stage:{stage}  | {uiInfo}  | {worldInfo}  | {trig}");
    }
}
