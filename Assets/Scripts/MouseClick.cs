using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple mouse click class used to showcase the melt toggle
/// </summary>
[RequireComponent(typeof(Camera))]
public class MouseClick : MonoBehaviour
{
    [SerializeField, HideInInspector] private Camera cam;
    [SerializeField, HideInInspector] private InputAction clickAction;
    [SerializeField] private float reEnableTime = 5;
    private readonly List<(Collider col, float time)> registeredCollidersToReEnable = new();

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        this.cam ??= GetComponent<Camera>();
        this.clickAction ??= new InputAction(name: "Click", binding: "<Mouse>/leftButton");
    }

    private void Awake() => this.clickAction.performed += OnClick;

    private void OnEnable() => this.clickAction.Enable();
    private void OnDisable() => this.clickAction.Disable();

    private void OnClick(InputAction.CallbackContext context)
    {
        if (Mouse.current == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = this.cam.ScreenPointToRay(mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit)) return;
        
        MeltFXToggle meltFX = hit.collider.gameObject.GetComponent<MeltFXToggle>();
        if (!meltFX) return;
        
        hit.collider.enabled = false;
        this.registeredCollidersToReEnable.Add(new ValueTuple<Collider, float>(hit.collider, Time.time));
        
        meltFX.SetEffect(true);
        meltFX.DelayedToggleState(this.reEnableTime);
    }

    private void Update()
    {
        for (int i = this.registeredCollidersToReEnable.Count - 1; i >= 0; i--)
        {
            float registeredTime = this.registeredCollidersToReEnable[i].Item2;
            float elapsedTime = Time.time - registeredTime;
            if (elapsedTime < this.reEnableTime) continue;
            this.registeredCollidersToReEnable[i].Item1.enabled = true;
            this.registeredCollidersToReEnable.RemoveAt(i);
        }
    }
}