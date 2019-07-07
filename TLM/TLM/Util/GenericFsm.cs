namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A finite state machine
    /// </summary>
    /// <typeparam name="TState">Enum containing states for the FSM</typeparam>
    /// <typeparam name="TTrigger">Enum with trigger events for the FSM</typeparam>
    public class GenericFsm<TState, TTrigger> {
        public TState State { get; private set; }

        private readonly Dictionary<TState, Dictionary<TTrigger, TState>> transitions_;
        private readonly Dictionary<TState, Action> enterStateEvents_;

        public GenericFsm(TState state) {
            transitions_ = new Dictionary<TState, Dictionary<TTrigger, TState>>();
            enterStateEvents_ = new Dictionary<TState, Action>();
            State = state;
            // Log._Debug($"FSM: Created with state {state}");
        }

        /// <summary>
        /// Used temporarily to set up the GenericFsm
        /// </summary>
        public class Configurator {
            private GenericFsm<TState, TTrigger> fsm_;

            private TState state_;

            public Configurator(GenericFsm<TState, TTrigger> fsm, TState state) {
                fsm_ = fsm;
                state_ = state;
            }

            /// <summary>
            /// Calling this sets up an allowed FSM transition to the new state
            /// </summary>
            /// <param name="t">Trigger which will allow transition from the current to the new state</param>
            /// <param name="toState">The new state</param>
            /// <returns>This</returns>
            public Configurator Permit(TTrigger t, TState toState) {
                fsm_.Permit(state_, t, toState);
                return this;
            }

            /// <summary>
            /// Set a callback for when a state is activated. NOTE: will not trigger on initial state.
            /// </summary>
            /// <param name="action">Event to run when state is entered</param>
            /// <returns>This</returns>
            public Configurator OnEntry(Action action) {
                fsm_.OnEntry(state_, action);
                return this;
            }
        }

        private void OnEntry(TState state, Action action) {
            enterStateEvents_.Add(state, action);
        }

        /// <summary>
        /// Set up an allowed transition (unconditional)
        /// </summary>
        /// <param name="fromState">State FSM has to be in</param>
        /// <param name="trigger">Trigger event (sent from the outside)</param>
        /// <param name="toState">State FSM will switch to</param>
        private void Permit(TState fromState, TTrigger trigger, TState toState) {
            if (!transitions_.ContainsKey(fromState)) {
                transitions_.Add(fromState, new Dictionary<TTrigger, TState>());
            }

            transitions_[fromState].Add(trigger, toState);
        }

        public Configurator Configure(TState state) {
            return new Configurator(this, state);
        }

        /// <summary>
        /// FSM takes a trigger event and possibly changes a state
        /// </summary>
        /// <param name="trigger">A trigger event</param>
        /// <returns>Whether state change succeeded</returns>
        public bool SendTrigger(TTrigger trigger) {
            if (!transitions_.ContainsKey(State)) {
                // Log._Debug($"FSM: Can't leave state {State} with {trigger} - " +
                //           "no transitions defined for it");
                return false;
            }

            var outTransitions = transitions_[State];
            if (!outTransitions.ContainsKey(trigger)) {
                // Log._Debug($"FSM: Can't leave state {State} with {trigger} - " +
                //           "the trigger is not accepted in this state");
                return false;
            }

            // Log._Debug($"FSM: State changed {State} -> ({trigger}) -> {outTransitions[trigger]}");
            State = outTransitions[trigger];

            // Callback on enter state
            if (enterStateEvents_.ContainsKey(State)) {
                enterStateEvents_[State].Invoke();
            }

            return true;
        }
    }
}