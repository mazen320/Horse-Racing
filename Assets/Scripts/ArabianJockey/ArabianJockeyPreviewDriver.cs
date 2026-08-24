using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quick preview controls for the imported ArabianHorseJockey prefabs (legacy Animation component).
/// The pack has no Sprint clip, so Sprint plays looping Gallop faster.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animation))]
public class ArabianJockeyPreviewDriver : MonoBehaviour
{
	[SerializeField] Animation m_animation;
	[SerializeField] float m_walkSpeed = 0.8f;
	[SerializeField] float m_trotSpeed = 1.4f;
	[SerializeField] float m_gallopSpeed = 2.2f;
	[SerializeField] float m_sprintSpeed = 3.4f;
	[SerializeField, Range(1f, 2f)] float m_sprintAnimSpeed = 1.45f;
	[SerializeField] float m_turnSpeed = 55f;
	[Tooltip("Use IJKL so controls don't fight the spline race horse (Space/W taps). I+O Gallop, I+P Sprint.")]
	[SerializeField] bool m_useIJKLKeys = true;

	static readonly string[] s_clips =
	{
		"Idlle", "Idle01", "Idle02", "Idle03", "Trot", "Gallop", "WalkBack", "Jump", "Attack"
	};

	static readonly string[] s_loopingClips =
	{
		"Idlle", "Idle01", "Idle02", "Idle03", "Trot", "Gallop", "WalkBack"
	};

	void Reset()
	{
		m_animation = GetComponent<Animation>();
	}

	void Awake()
	{
		if (m_animation == null)
			m_animation = GetComponent<Animation>();
	}

	void Start()
	{
		if (m_animation == null)
			return;

		ConfigureLoopingClips();

		if (m_animation.GetClip("Idlle") != null)
			m_animation.Play("Idlle");
		else if (m_animation.clip != null)
			m_animation.Play();
	}

	void Update()
	{
		if (m_animation == null)
			return;

		for (int i = 0; i < s_clips.Length && i < 9; i++)
		{
			if (KeyDown((Key)((int)Key.Digit1 + i)))
				PlayClip(s_clips[i], 1f);
		}

		if (KeyDown(Key.U))
			PlayClip("Jump", 1f);

		float throttle = 0f;
		float steer = 0f;
		if (m_useIJKLKeys)
		{
			if (Held(Key.I)) throttle = 1f;
			if (Held(Key.K)) throttle = -1f;
			if (Held(Key.J)) steer = -1f;
			if (Held(Key.L)) steer = 1f;
		}
		else
		{
			if (Held(Key.W) || Held(Key.UpArrow)) throttle = 1f;
			if (Held(Key.S) || Held(Key.DownArrow)) throttle = -1f;
			if (Held(Key.A) || Held(Key.LeftArrow)) steer = -1f;
			if (Held(Key.D) || Held(Key.RightArrow)) steer = 1f;
			if (KeyDown(Key.Space))
				PlayClip("Jump", 1f);
		}

		bool sprint = Held(Key.P);
		bool gallop = Held(Key.O);

		if (throttle > 0.1f)
		{
			if (sprint)
				PlayClip("Gallop", m_sprintAnimSpeed);
			else
				PlayClip(gallop ? "Gallop" : "Trot", 1f);
		}
		else if (throttle < -0.1f)
			PlayClip("WalkBack", 1f);

		if (Mathf.Abs(steer) > 0.01f)
			transform.Rotate(0f, steer * m_turnSpeed * Time.deltaTime, 0f, Space.World);

		float moveSpeed = m_trotSpeed;
		if (sprint && throttle > 0.1f)
			moveSpeed = m_sprintSpeed;
		else if (gallop && throttle > 0.1f)
			moveSpeed = m_gallopSpeed;
		else if (throttle < -0.1f)
			moveSpeed = m_walkSpeed;

		float move = throttle * moveSpeed * Time.deltaTime;
		if (Mathf.Abs(move) > 0f)
			transform.position += transform.forward * move;
	}

	void ConfigureLoopingClips()
	{
		for (int i = 0; i < s_loopingClips.Length; i++)
		{
			var state = m_animation[s_loopingClips[i]];
			if (state == null)
				continue;

			state.wrapMode = WrapMode.Loop;
			state.speed = 1f;
		}
	}

	void PlayClip(string clipName, float animSpeed)
	{
		if (m_animation.GetClip(clipName) == null)
			return;

		var state = m_animation[clipName];
		if (state != null)
		{
			state.speed = animSpeed;
			if (IsLoopingClip(clipName))
				state.wrapMode = WrapMode.Loop;
		}

		if (!m_animation.IsPlaying(clipName))
			m_animation.CrossFade(clipName, 0.15f);
	}

	static bool IsLoopingClip(string clipName)
	{
		for (int i = 0; i < s_loopingClips.Length; i++)
		{
			if (s_loopingClips[i] == clipName)
				return true;
		}

		return false;
	}

	static bool Held(Key key)
	{
		var kb = Keyboard.current;
		return kb != null && kb[key].isPressed;
	}

	static bool KeyDown(Key key)
	{
		var kb = Keyboard.current;
		return kb != null && kb[key].wasPressedThisFrame;
	}
}
