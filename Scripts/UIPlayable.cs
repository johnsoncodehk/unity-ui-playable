using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Events;

using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using System.Reflection;
using System.Reflection.Emit;

[CustomEditor(typeof(UIPlayable))]
public class UIPlayableInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var controller = target as UIPlayable;

        int defaultStateIndex = controller.states.FindIndex(state => !string.IsNullOrEmpty(state.name) && state.name == controller.defaultState);
        bool defaultStateMissing = !string.IsNullOrEmpty(controller.defaultState) && defaultStateIndex == -1;
        List<string> options = new List<string> { "None" };
        foreach (UIPlayable.State state in controller.states)
        {
            if (string.IsNullOrEmpty(state.name))
            {
                options.Add("");
                options.Add("");
            }
            else
            {
                options.Add(state.name);
                options.Add(state.name + " (On Enter)");
            }
        }

        int currentIndex = 0;
        if (defaultStateMissing)
        {
            options.Add(("Missing (" + controller.defaultState + ")"));
            currentIndex = options.Count - 1;
        }
        else if (defaultStateIndex >= 0)
        {
            currentIndex = defaultStateIndex * 2 + 1;
            if (controller.defaultStateAnimation == UIPlayable.StateAnimation.Loop)
                currentIndex += 1;
        }

        int newIndex = EditorGUILayout.Popup("Default State", currentIndex, options.ToArray());
        if (newIndex != currentIndex)
        {
            if (newIndex == 0)
            {
                controller.defaultState = "";
                controller.defaultStateAnimation = UIPlayable.StateAnimation.Enter;
            }
            else
            {
                UIPlayable.State state = controller.states[(newIndex - 1) / 2];
                controller.defaultState = state.name;
                controller.defaultStateAnimation = newIndex % 2 == 1 ? UIPlayable.StateAnimation.Enter : UIPlayable.StateAnimation.Loop;
            }
            EditorUtility.SetDirty(controller);
        }
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("states"), true);
        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();
            foreach (UIPlayable.State state in controller.states)
                if (GUILayout.Button(state.name))
                    controller.Play(state);
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif

[RequireComponent(typeof(Animator))]
public class UIPlayable : MonoBehaviour
{

#if UNITY_EDITOR
    void OnValidate()
    {
        AnimatorController preview = animator.runtimeAnimatorController as AnimatorController;
        if (!preview || preview.name != "UIPlayable Preview")
        {
            preview = new AnimatorController();
            preview.name = "UIPlayable Preview";
            preview.hideFlags = HideFlags.DontSave;
            preview.AddLayer("Base Layer");
            preview.AddLayer("Preview Layer");
            preview.layers[1].defaultWeight = 0;

            animator.runtimeAnimatorController = preview;
        }

        foreach (var childAnimatorState in preview.layers[1].stateMachine.states)
        {
            var animatorState = childAnimatorState.state;
            bool exist = states.Any(state => state.enterAnimation == animatorState.motion || state.loopAnimation == animatorState.motion);
            if (!exist)
            {
                preview.RemoveLayer(1);
                preview.AddLayer("Preview Layer");
                preview.layers[1].defaultWeight = 0;
                break;
            }
        }
        foreach (State state in states)
        {
            if (state.enterAnimation) AddMotionIfNotExist(preview, state.enterAnimation);
            if (state.loopAnimation) AddMotionIfNotExist(preview, state.loopAnimation);
        }
    }
    static void AddMotionIfNotExist(AnimatorController preview, AnimationClip clip)
    {
        bool exist = preview.animationClips.Any(controllerClip => controllerClip == clip);
        if (!exist)
        {
            var motion = preview.AddMotion(clip, 1);
            motion.name = clip.name;
        }
    }
#endif

    public enum StateAnimation
    {
        Enter,
        Loop,
    }
    // public enum StateEnterAction
    // {
    //     None,
    //     Enable,
    // }
    // public enum StateOnEnterAction
    // {
    //     None,
    //     Disable,
    //     Destroy,
    // }
    [Serializable]
    public class State
    {
        public string name;
        // public StateEnterAction enterAction;
        public AnimationClip enterAnimation;
        // public StateOnEnterAction onEnterAction;
        public AnimationClip loopAnimation;
        public UnityEvent onEnter = new UnityEvent();
        [HideInInspector] public UnityEvent m_OnEnterDisposable = new UnityEvent();
    }
    [Serializable]
    class PlayingState
    {
        public bool looping;
        public PlayableGraph playableGraph;
        public AnimationMixerPlayable rootMixerPlayable;
        public State state;
        public AnimationClipPlayable clipPlayable;

        public bool TryStartLoop()
        {
            if (looping)
                return false;
            if (state.enterAnimation && clipPlayable.GetTime() < state.enterAnimation.length)
                return false;

            StartLoop();
            return true;
        }
        public void StartLoop()
        {
            looping = true;

            if (state.loopAnimation)
            {
                clipPlayable.Destroy();
                clipPlayable = AnimationClipPlayable.Create(playableGraph, state.loopAnimation);
                playableGraph.Connect(clipPlayable, 0, rootMixerPlayable, 0);
            }
            else if (state.enterAnimation)
            {
                clipPlayable.SetTime(state.enterAnimation.length);
            }
        }
        public void Destroy()
        {
            clipPlayable.Destroy();
        }
        public PlayingState(PlayableGraph playableGraph, AnimationMixerPlayable rootMixerPlayable, State state)
        {
            this.state = state;
            this.playableGraph = playableGraph;
            this.rootMixerPlayable = rootMixerPlayable;

            clipPlayable = AnimationClipPlayable.Create(playableGraph, state.enterAnimation);
            playableGraph.Connect(clipPlayable, 0, rootMixerPlayable, 0);
            rootMixerPlayable.SetInputWeight(0, 1);
            clipPlayable.Play();
        }
    }

    public string defaultState;
    public StateAnimation defaultStateAnimation;
    public List<State> states = new List<State>();
    // public List<State> states = new List<State>() {
    //     new State {
    //         name = "Show",
    //         enterAction = StateEnterAction.Enable,
    //         onEnterAction = StateOnEnterAction.None,
    //     },
    //     new State {
    //         name = "Hide",
    //         enterAction = StateEnterAction.None,
    //         onEnterAction = StateOnEnterAction.Disable,
    //     },
    // };

    public bool isPlaying
    {
        get
        {
            return m_PlayingState != null && !m_PlayingState.looping;
        }
    }
    public Animator animator
    {
        get
        {
            if (this.m_Animator == null)
            {
                this.m_Animator = this.GetComponent<Animator>();
            }
            return this.m_Animator;
        }
    }
    public State currentState
    {
        get
        {
            return m_PlayingState == null ? null : m_PlayingState.state;
        }
    }

    int m_LastSetActiveFrame = -1;
    Animator m_Animator;
    PlayableGraph m_PlayableGraph;
    AnimationPlayableOutput m_PlayableOutput;
    AnimationMixerPlayable m_RootMixerPlayable;
    PlayingState m_PlayingState;

    public virtual void Play(State state)
    {
        // if (!gameObject.activeSelf && state.enterAction != StateEnterAction.Enable)
        //     return;

        // if (!gameObject.activeSelf && state.enterAction == StateEnterAction.Enable)
        // {
        //     m_LastSetActiveFrame = Time.frameCount;
        //     gameObject.SetActive(true);
        // }
        if (m_PlayableGraph.IsValid())
        {
            if (m_PlayingState != null)
                m_PlayingState.Destroy();
            m_PlayingState = new PlayingState(m_PlayableGraph, m_RootMixerPlayable, state);
            Update();
        }
    }
    public void Play(State state, UnityAction onEnterAction)
    {
        if (onEnterAction != null)
            state.onEnter.AddListener(onEnterAction);
        Play(state);
    }
    public void Play(string stateName)
    {
        State state = states.FirstOrDefault(state_2 => state_2.name == stateName);
        Assert.IsNotNull(state);
        Play(state);
    }
    public void Play(string stateName, UnityAction onEnterAction)
    {
        State state = states.FirstOrDefault(state_2 => state_2.name == stateName);
        Assert.IsNotNull(state);
        Play(state, onEnterAction);
    }

    protected virtual void OnEnable()
    {
        CreateGraph();

        if (Time.frameCount != m_LastSetActiveFrame)
        {
            if (!string.IsNullOrEmpty(defaultState))
            {
                State state = states.FirstOrDefault(state_2 => state_2.name == defaultState);
                if (state != null)
                {
                    Play(state);
                    if (defaultStateAnimation == StateAnimation.Loop)
                        m_PlayingState.StartLoop();
                }
            }
        }
        m_LastSetActiveFrame = -1;
    }
    protected virtual void OnDisable()
    {
        m_PlayingState = null;
        m_PlayableGraph.Destroy();
    }
    protected virtual void Update()
    {
        if (m_PlayingState != null)
            if (!m_PlayingState.looping)
                if (!m_PlayingState.state.enterAnimation || m_PlayingState.clipPlayable.GetTime() >= m_PlayingState.state.enterAnimation.length)
                {
                    m_PlayingState.StartLoop();
                    OnPlay(m_PlayingState.state);
                }
    }
    protected virtual void OnPlay(State state)
    {
        // switch (state.onEnterAction)
        // {
        //     case StateOnEnterAction.None:
        //         break;
        //     case StateOnEnterAction.Disable:
        //         if (m_PlayableGraph.IsValid())
        //         {
        //             m_RootMixerPlayable.SetInputWeight(0, 0);
        //             m_PlayableGraph.Evaluate();
        //         }
        //         gameObject.SetActive(false);
        //         break;
        //     case StateOnEnterAction.Destroy:
        //         Destroy(gameObject);
        //         break;
        // }
        state.onEnter.Invoke();
        state.m_OnEnterDisposable.Invoke();
        state.m_OnEnterDisposable.RemoveAllListeners();
    }

    void CreateGraph()
    {
        m_PlayableGraph = PlayableGraph.Create(name);
        m_PlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        m_PlayableOutput = AnimationPlayableOutput.Create(m_PlayableGraph, "Animation", animator);
        m_RootMixerPlayable = AnimationMixerPlayable.Create(m_PlayableGraph, 1);
        m_PlayableOutput.SetSourcePlayable(m_RootMixerPlayable);

        m_PlayableGraph.Play();
    }
}
