using UnityEngine;

namespace CodeScape
{
    // Small procedurally generated sounds, so the project needs no audio assets.
    public class AudioFeedback : MonoBehaviour
    {
        public static AudioFeedback Instance { get; private set; }

        AudioSource source;
        AudioClip click, blip, success, error;

        void Awake()
        {
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.35f;

            click   = Tone("click", 0.05f, 880f, 880f);
            blip    = Tone("blip", 0.09f, 660f, 990f);
            success = Tone("success", 0.35f, 523f, 1046f);
            error   = Tone("error", 0.35f, 330f, 150f);
        }

        public static void Click()   => Play(i => i.click);
        public static void Blip()    => Play(i => i.blip);
        public static void Success() => Play(i => i.success);
        public static void Error()   => Play(i => i.error);

        static void Play(System.Func<AudioFeedback, AudioClip> pick)
        {
            if (Instance != null) Instance.source.PlayOneShot(pick(Instance));
        }

        static AudioClip Tone(string name, float seconds, float startHz, float endHz)
        {
            const int rate = 44100;
            int samples = Mathf.CeilToInt(rate * seconds);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float hz = Mathf.Lerp(startHz, endHz, t);
                phase += 2f * Mathf.PI * hz / rate;
                float envelope = Mathf.Min(1f, t * 20f) * (1f - t);
                data[i] = Mathf.Sin(phase) * envelope;
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
