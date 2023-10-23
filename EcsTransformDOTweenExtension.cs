using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

public static class EcsTransformDOTweenExtension
{
    public static TweenerCore<Vector3, Vector3, VectorOptions> DOLocalMove(this EntityView target, Vector3 endValue, float duration, bool snapping = false)
    {
        TweenerCore<Vector3, Vector3, VectorOptions> tweenerCore = DOTween.To(
            () => target.LocalPosition,
            (x) => target.LocalPosition = x,
            endValue,
            duration);
        tweenerCore.SetOptions(snapping).SetTarget(target);
        return tweenerCore;
    }

    public static TweenerCore<Quaternion, Vector3, QuaternionOptions> DOLocalRotate(this EntityView target, Vector3 endValue, float duration, RotateMode mode = RotateMode.Fast)
    {
        TweenerCore<Quaternion, Vector3, QuaternionOptions> tweenerCore = DOTween.To(
            () => target.LocalRotation,
            (x) => target.LocalRotation = x,
            endValue,
            duration);
        tweenerCore.SetTarget(target);
        tweenerCore.plugOptions.rotateMode = mode;
        return tweenerCore;
    }
}
