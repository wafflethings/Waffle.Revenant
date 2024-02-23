using System.Collections;
using System.Linq;
using UnityEngine;
using Waffle.Revenant.States;

namespace Waffle.Revenant
{
    public class Revenant : MonoBehaviour
    {
        static Revenant()
        {
            // i would keep it in this class but rude is janky ;3
            Patches.Patch();
        }

        [Header("Spawn Animation (feel free to choose this)")]
        public RevenantSpawn SpawnType = RevenantSpawn.StompFromSky;
        public GameObject SpawnEffect;

        [Header("Jumpscares")]
        public bool JumpscaresEnabled = true;
        public bool JumpscareImages = false;
        public Sprite[] JumpscarePool;
        public Sprite[] EnragedJumpscarePool;

        [Header("Materials")]
        public Material DefaultMaterial;
        public Material EnragedMaterial;

        [Header("Swing Attacks")]
        public int SwingDamage = 30;
        public SwingCheck2 HeadSwing;
        public SwingCheck2 LeftArmSwing;
        public SwingCheck2 RightArmSwing;

        [Header("Projectile Attack")]
        public GameObject ProjectileDecorative;
        public GameObject Projectile;
        public GameObject LeftYTracker;
        public GameObject RightYTracker;
        public GameObject ProjectileSpawn;

        [Header("Shockwave Stomp")]
        public GameObject ShockwaveIntro;
        public GameObject ShockwaveAttack;
        public GameObject Hole;

        [Header("Sound Effects")]
        public GameObject BaseSoundBubble;
        public AudioClip[] SwingAttackSounds;
        public AudioClip VisibilitySound;
        public AudioClip InvisibilitySound;
        public AudioClip StompSound;

        [Header("Lighting")]
        public Light Glow;
        public Color DefaultColour;
        public Color RageColour;

        [Header("Misc")]
        public GameObject ParryFlash;
        public GameObject NoParryFlash;
        public GameObject EnragedEffect;
        public GameObject DeathEffect;
        public GameObject SeasonalHatScaler;
        private float _glowStartRange;

        public static float DownOffsetMultiplier = 2.5f;
        [HideInInspector] public RevenantState RevState = null;
        [HideInInspector] public Machine Machine;
        [HideInInspector] public float ForwardBoost;
        [HideInInspector] public bool LookAtPlayer;
        [HideInInspector] public bool ResetRotation;
        [HideInInspector] public Quaternion TargetRotation;
        [HideInInspector] public bool Enraged;
        [HideInInspector] public GameObject CurrentEnragedEffect;
        private bool _hasStomped = false;
        private Vector3 _currentPredicted;
        private bool _trackPlayer = false;
        private int _difficulty;

        public float SpeedMultiplier
        {
            get
            {
                float mult = (Machine.eid?.totalSpeedModifier) ?? 1;
                mult += (_difficulty <= 1 ? -0.15f : 0);
                mult += (_difficulty >= 4 ? 0.15f : 0);
                mult += (Enraged ? 0.5f : 0);
                return mult;
            }
        }

        private float _frameTime
        {
            get
            {
                return (1f / 24f) / SpeedMultiplier;
            }
        }

        public void Start()
        {
            Machine = GetComponent<Machine>();
            Machine.anim = GetComponentInChildren<Animator>();
            UpdateBuff();

            _glowStartRange = Glow.range;

            if (Machine.enabled)
            {
                Debug.LogError("The Revenant Machine component should be off by default.");
            }

            if (SpawnType == RevenantSpawn.StompFromSky)
            {
                Machine.anim.SetBool("Stomp First Frame", true);
            }
            else
            {
                if (SpawnType == RevenantSpawn.AppearFromInvisibility)
                {
                    StartCoroutine(GoVisible());
                }

                if (SpawnType == RevenantSpawn.Standard)
                {
                    SpawnEffect.SetActive(true);
                }

                Machine.enabled = true;
            }

            if (Machine.GetComponent<EnemyIdentifier>().difficultyOverride >= 0)
            {
                _difficulty = Machine.GetComponent<EnemyIdentifier>().difficultyOverride;
            }
            else
            {
                _difficulty = PrefsManager.Instance.GetInt("difficulty", 0);
            }
        }

        // called by some sendmessage, thanks hakito
        public void UpdateBuff()
        {
            int newDamage = (int)(SwingDamage * (Machine.eid?.totalDamageModifier ?? 1f));
            HeadSwing.damage = newDamage;
            LeftArmSwing.damage = newDamage;
            RightArmSwing.damage = newDamage;

            Machine.anim.speed = SpeedMultiplier;
        }

        public void Update()
        {
            if (Machine.eid?.dead ?? false)
            {
                return;
            }

            if (SpawnType == RevenantSpawn.StompFromSky && !_hasStomped)
            {
                return;
            }

            if (ULTRAKILL.Cheats.BlindEnemies.Blind)
            {
                if (RevState != null)
                {
                    RevState.End();
                }

                return;
            }

            if (RevState == null || (RevState?.Complete ?? false))
            {
                DecideState();
            }

            RevState.Update();

            bool shouldRotate = true;

            if (RevState.GetType() == typeof(PassiveState))
            {
                shouldRotate = false;
            }

            if (RevState.GetType() == typeof(RandomMeleeState) && (RevState as RandomMeleeState).AttackDone)
            {
                shouldRotate = false;
            }
            
            if (!LookAtPlayer && !ResetRotation)
            {
                shouldRotate = false;
            }

            if (_trackPlayer)
            {
                _currentPredicted = NewMovement.Instance.transform.position;
            }

            if (shouldRotate)
            {
                Quaternion oldRot = transform.rotation;
                if (LookAtPlayer)
                {
                    InstantLookAtPlayerPredicted();
                    TargetRotation = transform.rotation;
                    transform.rotation = oldRot;
                }

                if (ResetRotation)
                {
                    Vector3 oldRotVec = oldRot.eulerAngles;
                    oldRotVec.x = 0;
                    TargetRotation = Quaternion.Euler(oldRotVec);
                }

                transform.rotation = Quaternion.RotateTowards(transform.rotation, TargetRotation, Time.deltaTime * 240);
            }

            if (ForwardBoost != 0)
            {
                float distance = Vector3.Distance(transform.position, NewMovement.Instance.transform.position);
                float deacceleration = 40;

                if (distance < 60)
                {
                    deacceleration -= 15;
                }

                if (distance < 30)
                {
                    deacceleration += 20;
                }

                if (distance < 15)
                {
                    deacceleration += 20;
                }

                if (distance < 5)
                {
                    deacceleration += 50;
                }

                ForwardBoost = Mathf.MoveTowards(ForwardBoost, 0, Time.deltaTime * deacceleration * SpeedMultiplier);
                transform.position += transform.forward * (ForwardBoost * Time.deltaTime * SpeedMultiplier);
            }

            Vector3 projectileSpawnPos = (LeftYTracker.transform.position + RightYTracker.transform.position) / 2;
            ProjectileSpawn.transform.position = projectileSpawnPos + (ProjectileSpawn.transform.forward * 5);

            if (ForwardBoost > 0)
            {
                Vector3 normal = Vector3.zero;
                if (Physics.Raycast(transform.position + Vector3.up * 3, transform.forward, out RaycastHit hit1, 2f, LayerMaskDefaults.Get(LMD.Environment)))
                {
                    normal = hit1.normal;
                }
                else if (Physics.Raycast(transform.position, transform.forward + Vector3.up, out RaycastHit hit2, 2f, LayerMaskDefaults.Get(LMD.Environment)))
                {
                    normal = hit2.normal;
                }
                else if (Physics.Raycast(transform.position + Vector3.up * 6, transform.forward, out RaycastHit hit3, 2f, LayerMaskDefaults.Get(LMD.Environment)))
                {
                    normal = hit3.normal;
                }

                if (normal != Vector3.zero)
                {
                    Reflect(normal);
                }
            }
        }

        public void Reflect(Vector3 normal)
        {
            Debug.Log($"Reflecting, normal {normal}");
            transform.forward = Vector3.Reflect(transform.forward, normal);
            TargetRotation = transform.rotation;

            //just to prevent it going into the floor again 
            _currentPredicted = NewMovement.Instance.transform.position;
        }

        public void OnCollisionEnter(Collision col)
        {
            if (RevState.GetState(out RandomMeleeState rms))
            {
                ContactPoint[] filteredContacts = col.contacts.Where(col => LayerMaskDefaults.Get(LMD.Environment) == (LayerMaskDefaults.Get(LMD.Environment) | (1 << col.otherCollider.gameObject.layer))).ToArray();

                if (filteredContacts.Length > 0)
                {
                    if (Physics.Raycast(transform.position, filteredContacts[0].point - transform.position, out RaycastHit hit, 100, LayerMaskDefaults.Get(LMD.Environment)))
                    {
                        Reflect(hit.normal);
                    }
                }
            }
        }

        public void StompAnimation()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1000, LayerMaskDefaults.Get(LMD.Environment)))
            {
                StartCoroutine(StompEntrance(hit.point + transform.forward * 0.1f));
            }
        }

        public IEnumerator StompAttack(Vector3 position, AudioSource source)
        {
            yield return StartCoroutine(GoVisible());
            Machine.anim.SetBool("Stomp First Frame", true);
            yield return new WaitForSeconds(0.1f);
            CreateParryFlash(ParryFlash);
            Machine.parryable = true;
            yield return StompFall(position, 2, true, false, false, source);
            Machine.parryable = false;
            Instantiate(ShockwaveAttack, transform.position, Quaternion.identity);
        }

        public IEnumerator StompEntrance(Vector3 position)
        {
            Machine.enabled = true;
            yield return null; // have to wait a frame so Start has the time to get called, this code is aids :3

            CreateSoundClip(StompSound, out AudioSource source, false, true);
            yield return StompFall(position, 1, false, false, false, source);
            source.Stop();
            Instantiate(ShockwaveIntro, transform.position, Quaternion.identity);

            _hasStomped = true;
        }

        public IEnumerator StompFall(Vector3 position, float speedMultiplier = 1, bool lockAtPlayer = false, bool doSplatter = false, bool doSplatterDamage = false, AudioSource source = null)
        {
            speedMultiplier *= SpeedMultiplier;

            transform.LookAt(position);
            if (!doSplatter)
            {
                transform.rotation *= Quaternion.Euler(-90, 0, 0);
            }
            else
            {
                transform.Rotate(0, 180, 0, Space.Self);
                position += transform.forward;
            }

            float pointOffset = 3;
            float rateOfFall = 15f;
            float timeToWait = _frameTime * 33 / speedMultiplier;

            if (!doSplatter)
            {
                Machine.anim.SetBool("Stomp First Frame", false);
            }
            else
            {
                Machine.anim.Play("Stomp Air");
                rateOfFall = 50;
                timeToWait = 0;
            }

            Machine.anim.speed = SpeedMultiplier * speedMultiplier;
            float time = 0;
            while (time < timeToWait)
            {
                if (lockAtPlayer)
                {
                    if (Physics.Raycast(transform.position, NewMovement.Instance.transform.position - transform.position, out RaycastHit hit, 1000, LayerMaskDefaults.Get(LMD.Environment)))
                    {
                        transform.LookAt(hit.point);
                        transform.rotation *= Quaternion.Euler(-90, 0, 0);
                        position = hit.point;
                    }
                }

                time += Time.deltaTime;
                yield return null;
            }
            Machine.anim.speed = SpeedMultiplier;

            source?.Play();
            bool hasDone = false;
            do
            {
                if (Vector3.Distance(transform.position, position) <= pointOffset && !hasDone)
                {
                    if (doSplatterDamage && !Enraged)
                    {
                        if (Physics.Raycast(transform.position, position - transform.position, out RaycastHit hit, 1000, LayerMaskDefaults.Get(LMD.Environment)))
                        {
                            transform.rotation = Quaternion.LookRotation(hit.normal);

                            if (hit.collider.tag == "Floor")
                            {
                                Machine.anim.SetTrigger("Splat Floor");
                            }
                            else
                            {
                                Machine.anim.SetTrigger("Splat Wall");
                            }
                        }
                        else
                        {
                            Machine.anim.SetTrigger("Splat Wall");
                        }
                    }
                    else
                    {
                        ResetRotation = true;
                        Machine.anim.SetTrigger("Stomp Land");
                    }

                    hasDone = true;
                }

                rateOfFall = Mathf.MoveTowards(rateOfFall, float.MaxValue, 75 * Time.deltaTime * speedMultiplier);
                transform.position = Vector3.MoveTowards(transform.position, position, rateOfFall * Time.deltaTime);

                yield return null;
            } 
            while (transform.position != position);
            source?.Stop();
        }

        public void InstantLookAtPlayer()
        {
            transform.LookAt(NewMovement.Instance.transform.position + (Vector3.down * DownOffsetMultiplier));
        }

        public void InstantLookAtPlayerPredicted()
        {
            transform.LookAt(_currentPredicted + (Vector3.down * DownOffsetMultiplier));
        }

        public void Enrage()
        {
            if (!Enraged)
            {
                float oldMaterial = Machine.smr.material.GetFloat("_OpacScale");
                Machine.smr.material = new(EnragedMaterial);
                Machine.smr.material.SetFloat("_OpacScale", oldMaterial);

                CurrentEnragedEffect = Instantiate(EnragedEffect);
                CurrentEnragedEffect.transform.parent = Machine.chest.transform;
                CurrentEnragedEffect.transform.localPosition = Vector3.zero;
                CurrentEnragedEffect.transform.localRotation = Quaternion.identity;
                Glow.color = RageColour;
                Enraged = true;

                UpdateBuff();
            }
        }

        public void Deenrage()
        {
            Destroy(CurrentEnragedEffect);

            Enraged = false;
            float oldMaterial = Machine.smr.material.GetFloat("_OpacScale");
            Machine.smr.material = new(DefaultMaterial);
            Machine.smr.material.SetFloat("_OpacScale", oldMaterial);
            Glow.color = DefaultColour;

            UpdateBuff();
        }

        public IEnumerator DeenrageAfterTime()
        {
            int seconds = (_difficulty + 1) * 3;

            for (int i = 0; i < seconds * 2; i++)
            {
                if (Random.value > 0.5f && !Machine.eid.dead)
                {
                    JumpscareCanvas.Instance.FlashImage(this);
                }
                yield return new WaitForSeconds(0.5f); //sums to 15s
            }
            
            Deenrage();
        }

        public void OnHurt(string origin)
        {
            if (Machine.eid.hitter == "coin" || origin.Contains("Coin.ReflectRevolver")) //this sucks but i cbf to write a transpiler so fuck you
            {
                Enrage();
                StartCoroutine(DeenrageAfterTime());
            }
        }

        public void DecideState()
        {
            if (Machine.eid.dead)
            {
                return;
            }

            if (RevState == null)
            {
                RevState = new PassiveState(this);
                return;
            }

            if (RevState.GetState(out StompState ss) && ss.ShouldCombo)
            {
                if (Random.value < 0.5)
                {
                    RevState = new RandomMeleeState(this, true);
                } 
                else
                {
                    RevState = new ProjectileAttackState(this);
                }
                return;
            }

            if (Vector3.Distance(NewMovement.Instance.transform.position, transform.position) <= 40)
            {
                if (Random.value < (!Enraged ? 0.75 : 0.5f))
                {
                    RevState = new RandomMeleeState(this);
                }
                else
                {
                    if (Random.value < 0.8)
                    {
                        CheckForStomp();
                    } 
                    else
                    {
                        RevState = new ProjectileAttackState(this);
                    }
                }

                return;
            }

            if (Vector3.Distance(NewMovement.Instance.transform.position, transform.position) > 40)
            {
                if (Random.value < 0.2 && !Enraged)
                {
                    RevState = new RandomMeleeState(this);
                }
                else
                {
                    CheckForStomp();
                }
                return;
            }

            RevState = new PassiveState(this);
        }

        public void CheckForStomp()
        {
            bool didHit = Physics.Raycast(NewMovement.Instance.transform.position, Vector3.up, out RaycastHit hit, 1000, LayerMaskDefaults.Get(LMD.Environment));
            RevState = new StompState(this, didHit ? hit.point : NewMovement.Instance.transform.position + Vector3.up * 25);
        }

        // called by Machine.GoLimp with a SendMessage, thanks Hakita
        public void Death()
        {
            GameObject deathEffect = Instantiate(DeathEffect);
            deathEffect.transform.parent = Machine.chest.transform;
            deathEffect.transform.localPosition = Vector3.zero;
            deathEffect.transform.localRotation = Quaternion.identity;

            ResetXRotation();
            StartCoroutine(DeathAnimation());
            Machine.anim.StopPlayback();
            RevState.End();
        }

        public void ResetXRotation()
        {
            Vector3 rotation = transform.rotation.eulerAngles;
            rotation.x = 0;
            transform.rotation = Quaternion.Euler(rotation);
        }

        public IEnumerator GoInvisible(float timeToWait = 0, bool invisAnimation = false)
        {
            yield return new WaitForSeconds(timeToWait);

            CreateSoundClip(InvisibilitySound, out AudioSource sound);
            sound.pitch = Random.Range(0.75f, 1.25f);

            if (invisAnimation)
            {
                Machine.anim.SetTrigger("Turn Invis");
                yield return new WaitForSeconds(0.25f / SpeedMultiplier);
            }

            while (Machine.smr.material.GetFloat("_OpacScale") != 0 && !Machine.eid.dead)
            {
                float targetScale = Mathf.MoveTowards(Machine.smr.material.GetFloat("_OpacScale"), 0, Time.deltaTime * 1.5f * SpeedMultiplier);
                Machine.smr.material.SetFloat("_OpacScale", targetScale);
                SeasonalHatScaler.transform.localScale = targetScale * Vector3.one;
                Glow.range = _glowStartRange * targetScale;
                yield return null;
            }

            if (Machine.eid.hooked)
            {
                HookArm.Instance.StopThrow(1f, true);
            }

            foreach (Nail nail in GetComponentsInChildren<Nail>())
            {
                StartCoroutine(DetachNail(nail));
            }
        }

        public IEnumerator DetachNail(Nail nail)
        {
            nail.transform.parent = null;
            nail.gameObject.AddComponent<Rigidbody>();
            yield return new WaitForSeconds(2);
            Destroy(nail.gameObject);
        }

        public IEnumerator GoVisible()
        {
            CreateSoundClip(VisibilitySound, out AudioSource sound);
            sound.pitch = Random.Range(0.75f, 1.25f);

            while (Machine.smr.material.GetFloat("_OpacScale") != 1)
            {
                float targetScale = Mathf.MoveTowards(Machine.smr.material.GetFloat("_OpacScale"), 1, Time.deltaTime * 3 * SpeedMultiplier);
                Machine.smr.material.SetFloat("_OpacScale", targetScale);
                SeasonalHatScaler.transform.localScale = targetScale * Vector3.one;
                Glow.range = _glowStartRange * targetScale;
                yield return null;
            }
        }

        public Sprite GetJumpscare()
        {
            return Enraged ? EnragedJumpscarePool[Random.Range(0, EnragedJumpscarePool.Length)] : JumpscarePool[Random.Range(0, JumpscarePool.Length)];
        }

        public IEnumerator Melee1(bool goVisible = true)
        {
            if (goVisible)
            {
                yield return StartCoroutine(GoVisible());
                yield return new WaitForSeconds(0.1f / SpeedMultiplier);
            }

            LookAtPlayer = true;

            Machine.anim.SetTrigger("Melee1");
            Machine.parryable = true;
            CreateParryFlash(ParryFlash);
            _trackPlayer = true;

            yield return new WaitForSeconds(_frameTime * 6); // 24fps, this is 6 frames

            ForwardBoost = 70f;
            if (Machine.parryable)
            {
                HeadSwing.DamageStart();
            }
            CreateRandomSwing();

            yield return new WaitForSeconds(_frameTime * 17);
            _trackPlayer = false;
            yield return new WaitForSeconds(_frameTime * 18); // totals to 41 frames

            Machine.parryable = false;
            HeadSwing.DamageStop();
            ResetRotation = true;
            ((RandomMeleeState)RevState).AttackDone = true;

            yield return new WaitForSeconds(_frameTime * 21); // totals to 62 frames
            LookAtPlayer = false;
            yield return StartCoroutine(GoInvisible());

            ResetXRotation();
            ResetRotation = false;
            ((RandomMeleeState)RevState).AttackDone = false;
        }

        public IEnumerator Melee2(bool goVisible = true)
        {
            if (goVisible)
            {
                yield return StartCoroutine(GoVisible());
                yield return new WaitForSeconds(0.1f / SpeedMultiplier);
            }

            LookAtPlayer = true;
            Machine.anim.SetTrigger("Melee2");
            Machine.parryable = false;
            CreateParryFlash(NoParryFlash);

            yield return new WaitForSeconds(_frameTime * 5); // 5 frames

            _currentPredicted = PlayerTracker.Instance.PredictPlayerPosition(0.5f);
            ForwardBoost = 60f;
            LeftArmSwing.DamageStart();
            CreateRandomSwing();

            yield return new WaitForSeconds(_frameTime * 5); // totals to 10 frames
            //LookAtPlayer = false;
            yield return new WaitForSeconds(_frameTime * 16); // totals to 26 frames

            LeftArmSwing.DamageStop();
            LookAtPlayer = false;
            Machine.parryable = true;
            CreateParryFlash(ParryFlash);
            InstantLookAtPlayer();

            yield return new WaitForSeconds(_frameTime * 5); // totals to 31 frames
            LookAtPlayer = true;
            _currentPredicted = PlayerTracker.Instance.PredictPlayerPosition(0.5f);

            ForwardBoost = 60f;
            if (Machine.parryable)
            {
                RightArmSwing.DamageStart();
            }
            CreateRandomSwing();

            yield return new WaitForSeconds(_frameTime * 3); // totals to 34 frames
            //LookAtPlayer = false;
            yield return new WaitForSeconds(_frameTime * 7); // totals to 41 frames

            LookAtPlayer = false;
            Machine.parryable = false;
            RightArmSwing.DamageStop();

            ResetRotation = true;
            ((RandomMeleeState)RevState).AttackDone = true;
            yield return new WaitForSeconds(_frameTime * 31); // totals to 72 frames
            yield return StartCoroutine(GoInvisible());
            ResetXRotation();
            ResetRotation = false;
            ((RandomMeleeState)RevState).AttackDone = false;
        }

        public IEnumerator Melee3(bool goVisible = true)
        {
            if (goVisible)
            {
                yield return StartCoroutine(GoVisible());
                yield return new WaitForSeconds(0.1f / SpeedMultiplier);
            }

            _currentPredicted = PlayerTracker.Instance.PredictPlayerPosition(1f);
            LookAtPlayer = true;
            Machine.anim.SetTrigger("Melee3");

            yield return new WaitForSeconds(_frameTime * 4); // 4 frames

            Machine.parryable = false;
            CreateParryFlash(NoParryFlash);

            yield return new WaitForSeconds(_frameTime * 3); // totals to 7 frames
            _trackPlayer = true;

            ForwardBoost = 60f;
            LeftArmSwing.DamageStart();
            RightArmSwing.DamageStart();
            CreateRandomSwing();

            yield return new WaitForSeconds(_frameTime * 5); // totals to 12 frames

            //LookAtPlayer = false;

            yield return new WaitForSeconds(_frameTime * 16); // totals to 28 frames

            _trackPlayer = false;
            LookAtPlayer = false;
            LeftArmSwing.DamageStop();
            RightArmSwing.DamageStop();

            ResetRotation = true;
            ((RandomMeleeState)RevState).AttackDone = true;
            yield return new WaitForSeconds(_frameTime * 24); // totals to 52 frames
            yield return StartCoroutine(GoInvisible());
            ResetXRotation();
            ResetRotation = false;
            ((RandomMeleeState)RevState).AttackDone = false;
        }

        public void CreateSoundClip(AudioClip clip, out AudioSource source, bool shouldPlay = true, bool loop = false)
        {
            GameObject sound = Instantiate(BaseSoundBubble, transform);
            source = sound.GetComponent<AudioSource>();
            source.loop = loop;
            source.clip = clip;
            sound.name = $"Sound Player: {clip.name}";

            if (shouldPlay)
            {
                source.Play();
            }
        }

        public void CreateRandomSwing()
        {
            CreateSoundClip(SwingAttackSounds[Random.Range(0, SwingAttackSounds.Length - 1)], out AudioSource source);
            source.pitch = Random.Range(0.5f, 1.5f);
        }

        public IEnumerator Stomp()
        {
            yield return StartCoroutine(GoVisible());
        }

        public IEnumerator RangedAttack()
        {
            _trackPlayer = true;
            LookAtPlayer = true;
            yield return StartCoroutine(GoVisible());
            Machine.anim.SetTrigger("Range Attack");
            GameObject decoProjectile = Instantiate(ProjectileDecorative, ProjectileSpawn.transform);
            if (RevState.GetState(out ProjectileAttackState pas))
            {
                pas.DecoProjectile = decoProjectile;
            }
            StartCoroutine(PrepareDecoProjectile(decoProjectile));
            yield return new WaitForSeconds(_frameTime * 25); //51 frames
            _trackPlayer = false;

            InstantLookAtPlayer();
            Destroy(decoProjectile);
            GameObject realProjectile = Instantiate(Projectile, ProjectileSpawn.transform.position, ProjectileSpawn.transform.rotation);
            realProjectile.transform.LookAt(CameraController.Instance.transform);
            Projectile projectile = realProjectile.GetComponent<Projectile>();
            projectile.target = Machine.eid.target;
            projectile.speed = 60f * SpeedMultiplier;

            foreach (Projectile subProj in realProjectile.GetComponentsInChildren<Projectile>())
            {
                subProj.damage *= Machine.eid.totalDamageModifier;
            }

            LookAtPlayer = false;
            ((ProjectileAttackState)RevState).AttackDone = true;

            ResetRotation = true;
            yield return new WaitForSeconds(_frameTime * 17); // 85
            yield return StartCoroutine(GoInvisible(0, true));
            ResetXRotation();
            ResetRotation = false;
            ((ProjectileAttackState)RevState).AttackDone = false;
        }

        public IEnumerator PrepareDecoProjectile(GameObject projectile)
        {
            //24 x 3 is 72. takes 1.5f seconds to prepare as it is sped up 2x
            float timeElapsed = 0;
            AudioSource source = projectile.GetComponent<AudioSource>();
            float startVol = source.volume;
            Vector3 startScale = projectile.transform.localScale;
            
            while (timeElapsed < 1.5f && projectile != null)
            {
                source.volume = startVol * (timeElapsed / 1.5f);
                projectile.transform.localScale = startScale * (timeElapsed / 1.5f);
                timeElapsed += Time.deltaTime * SpeedMultiplier;
                yield return null;
            }
        }

        public void CreateParryFlash(GameObject flash)
        {
            Instantiate(flash, Machine.chest.transform.position + transform.forward, transform.rotation).transform.localScale *= 2.5f;
        }

        // sendmessage in machine
        public void GotParried()
        {
            if (RevState.GetState(out StompState st) && !st.AttackDone && !Machine.eid.dead)
            {
                StartCoroutine(st.StartSpirallingAndSet());
            }

            Machine.parryable = false;
            HeadSwing.DamageStop();
            LeftArmSwing.DamageStop();
            RightArmSwing.DamageStop();
        }

        public IEnumerator DeathAnimation()
        {
            Deenrage();
            Machine.smr.material.SetFloat("_OpacScale", 1);
            Machine.anim.Play("Death");

            yield return new WaitForSeconds(_frameTime * 75); // i will die before i use anim events

            Machine.anim.enabled = false;
            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
    }

    public enum RevenantSpawn
    {
        StompFromSky,
        AppearFromInvisibility,
        Standard
    }
}
