using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quick preview controls for the imported ArabianHorseJockey prefabs (legacy Animation component).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animation))]
public class ArabianJockeyPreviewDriver : MonoBehaviour
{
	[SerializeField] Animation m_animation;
	[SerializeField] float m_walkSpeed = 0.8f;
	[SerializeField] float m_trotSpeed = 1.4f;
	[SerializeField] float m_gallopSpeed = 2.2f;
	[SerializeField] float m_turnSpeed = 55f;
	[Tooltip("Use IJKL so controls don't fight the spline race horse (Space/W taps).")]
	[SerializeField] bool m_useIJKLKeys = true;

	static readonly string[] s_clips =
	{
		"Idlle", "Idle01", "Idle02", "Idle03", "Trot", "Gallop", "WalkBack", "Jump", "Attack"
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
				PlayClip(s_clips[i]);
		}

		if (KeyDown(Key.U))
			PlayClip("Jump");

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
				PlayClip("Jump");
		}

		if (throttle > 0.1f)
			PlayClip(Held(Key.O) ? "Gallop" : "Trot");
		else if (throttle < -0.1f)
			PlayClip("WalkBack");

		if (Mathf.Abs(steer) > 0.01f)
			transform.Rotate(0f, steer * m_turnSpeed * Time.deltaTime, 0f, Space.World);

		float moveSpeed = m_trotSpeed;
		if (Held(Key.O) && throttle > 0.1f)
			moveSpeed = m_gallopSpeed;
		else if (throttle < -0.1f)
			moveSpeed = m_walkSpeed;

		float move = throttle * moveSpeed * Time.deltaTime;
		if (Mathf.Abs(move) > 0f)
			transform.position += transform.forward * move;
	}

	void PlayClip(string clipName)
	{
		if (m_animation.GetClip(clipName) == null)
			return;

		if (!m_animation.IsPlaying(clipName))
			m_animation.CrossFade(clipName, 0.15f);
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
