using System;
using UnityEngine;

public class CombatEventsBridge : MonoBehaviour
{
    public event Action OnWindupStart;
    public event Action OnHitStart;
    public event Action OnHitEnd;
    public event Action OnRecoverStart;
    public event Action OnComboOpen;
    public event Action OnComboClose;

    // Questi nomi DEVONO combaciare con gli eventi nelle clip
    public void WindupStart() => OnWindupStart?.Invoke();
    public void HitStart() => OnHitStart?.Invoke();
    public void HitEnd() => OnHitEnd?.Invoke();
    public void RecoverStart() => OnRecoverStart?.Invoke();
    public void ComboOpen() => OnComboOpen?.Invoke();
    public void ComboClose() => OnComboClose?.Invoke();
}
