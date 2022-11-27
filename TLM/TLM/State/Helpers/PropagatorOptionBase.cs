namespace TrafficManager.State.Helpers; 
using CSUtil.Commons;
using TrafficManager.State;
using JetBrains.Annotations;
using System.Collections.Generic;
using System;

public abstract class PropagatorOptionBase<TVal> : SerializableOptionBase<TVal>, IValuePropagator {
    private HashSet<IValuePropagator> _propagatesTrueTo = new();
    private HashSet<IValuePropagator> _propagatesFalseTo = new();
    protected PropagatorOptionBase(string name, Options.Scope scope) : base(name, scope) { }


    /// <summary>
    /// If this checkbox is set <c>true</c>, it will propagate that to the <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The checkbox to propagate <c>true</c> value to.</param>
    /// <remarks>
    /// If target is set <c>false</c>, it will propagate that back to this checkbox.
    /// </remarks>
    public void PropagateTrueTo([NotNull] IValuePropagator target) {
        Log.Info($"TriStateCheckboxOption.PropagateTrueTo: `{Name}` will propagate to `{target}`");
        this.AddPropagate(target, true);
        target.AddPropagate(this, false);
    }

    private HashSet<IValuePropagator> GetTargetPropagates(bool value) =>
        value ? _propagatesTrueTo : _propagatesFalseTo;

    public void AddPropagate(IValuePropagator target, bool value) =>
        GetTargetPropagates(value).Add(target);

    public abstract void Propagate(bool value);

    protected void PropagateAll(bool value) {
        foreach (var target in GetTargetPropagates(value))
            target.Propagate(value);
    }

    protected abstract void OnPropagateAll(TVal val);
}