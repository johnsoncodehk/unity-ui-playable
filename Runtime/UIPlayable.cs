using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

[RequireComponent(typeof(Animator))]
public class UIPlayable : MonoBehaviour
{

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;

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
            bool exist = states.Any(state => state.animation == animatorState.motion || state.loopAnimation == animatorState.motion);
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
            if (state.animation) AddMotionIfNotExist(preview, state.animation);
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

    public enum StateAnimationType
    {
        Enter,
        Loop,
    }
    [Serializable]
    public class State
    {
        public string name;
        public bool isDefaultState;
        [FormerlySerializedAs("enterAnimation")]
        public AnimationClip animation;
        public AnimationClip loopAnimation;
        [FormerlySerializedAs("onEnter")]
        public UnityEvent onAnimationEnd = new UnityEvent();
        [HideInInspector] public UnityEvent m_OnAnimationEndDisposable = new UnityEvent();
    }
    [Serializable]
    class PlayingStateInfo
    {
        public bool looping;
        public PlayableGraph playableGraph;
        public AnimationMixerPlayable rootMixerPlayable;
        public State state;
        public AnimationClipPlayable clipPlayable;

        public void PlayLoopAnimation()
        {
            looping = true;

            if (state.loopAnimation)
            {
                clipPlayable.Destroy();
                clipPlayable = AnimationClipPlayable.Create(playableGraph, state.loopAnimation);
                playableGraph.Connect(clipPlayable, 0, rootMixerPlayable, 0);
            }
            else if (state.animation)
            {
                clipPlayable.SetTime(state.animation.length);
            }
        }
        public void Destroy()
        {
            clipPlayable.Destroy();
        }
        public PlayingStateInfo(PlayableGraph playableGraph, AnimationMixerPlayable rootMixerPlayable, State state)
        {
            this.state = state;
            this.playableGraph = playableGraph;
            this.rootMixerPlayable = rootMixerPlayable;

            clipPlayable = AnimationClipPlayable.Create(playableGraph, state.animation);
            playableGraph.Connect(clipPlayable, 0, rootMixerPlayable, 0);
            rootMixerPlayable.SetInputWeight(0, 1);
            clipPlayable.Play();
        }
    }

    public List<State> states = new List<State>();
    public State defaultState
    {
        get
        {
            return states.Find(state => state.isDefaultState);
        }
        set
        {
            foreach (State state in states)
                state.isDefaultState = false;

            if (value != null)
                value.isDefaultState = true;
        }
    }
    public StateAnimationType defaultStateAnimation;

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
            return m_PlayingStateInfo == null ? null : m_PlayingStateInfo.state;
        }
    }

    Animator m_Animator;
    PlayableGraph m_PlayableGraph;
    AnimationPlayableOutput m_PlayableOutput;
    AnimationMixerPlayable m_RootMixerPlayable;
    PlayingStateInfo m_PlayingStateInfo;

    public virtual void Play(State state)
    {
        if (m_PlayableGraph.IsValid())
        {
            if (m_PlayingStateInfo != null)
                m_PlayingStateInfo.Destroy();
            m_PlayingStateInfo = new PlayingStateInfo(m_PlayableGraph, m_RootMixerPlayable, state);
            Update();
        }
    }
    public void Play(State state, UnityAction onEnterAction)
    {
        if (onEnterAction != null)
            state.m_OnAnimationEndDisposable.AddListener(onEnterAction);
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

        if (defaultState != null)
        {
            Play(defaultState);
            if (defaultStateAnimation == StateAnimationType.Loop)
                m_PlayingStateInfo.PlayLoopAnimation();
        }
    }
    protected virtual void OnDisable()
    {
        m_PlayingStateInfo = null;
        m_PlayableGraph.Destroy();
    }
    protected virtual void Update()
    {
        if (m_PlayingStateInfo != null)
            if (!m_PlayingStateInfo.looping)
                if (!m_PlayingStateInfo.state.animation || m_PlayingStateInfo.clipPlayable.GetTime() >= m_PlayingStateInfo.state.animation.length)
                {
                    m_PlayingStateInfo.PlayLoopAnimation();
                    OnPlay(m_PlayingStateInfo.state);
                }
    }
    protected virtual void OnPlay(State state)
    {
        state.onAnimationEnd.Invoke();
        state.m_OnAnimationEndDisposable.Invoke();
        state.m_OnAnimationEndDisposable.RemoveAllListeners();
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
