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
            Debug.LogWarning("Trying to patch, static constructor reached.");
            Patches.Patch();
        }

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

        [Header("Jumpscares")]
        public Sprite[] JumpscarePool;
        public Sprite[] EnragedJumpscarePool;

        [Header("Sound Effects")]
        public GameObject[] SwingAttackSounds;
        public GameObject ProjectileSound;
        public GameObject VisibilitySound;

        [Header("Misc")]
        public bool UseStompAnimation = true;
        public GameObject ParryFlash;
        public GameObject NoParryFlash;
        public GameObject EnragedEffect;

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

        public void Start()
        {
            Machine = GetComponent<Machine>();
            UpdateBuff();

            if (Machine.enabled)
            {
                Debug.LogError("The Revenant Machine component should be off by default.");
            }

            if (UseStompAnimation)
            {
                //for whatever reason, machine.anim is null here?
                GetComponentInChildren<Animator>().SetBool("Stomp First Frame", true);
            }
            else
            {
                Machine.enabled = true;
            }
        }

        // called by some sendmessage, thanks hakito
        public void UpdateBuff()
        {
            int newDamage = (int)(SwingDamage * (Machine.eid?.totalDamageModifier ?? 1f));
            HeadSwing.damage = newDamage;
            LeftArmSwing.damage = newDamage;
            RightArmSwing.damage = newDamage;
        }

        public void Update()
        {
            if (Machine.eid?.dead ?? false)
            {
                return;
            }

            if (UseStompAnimation && !_hasStomped)
            {
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

            if (ForwardBoost < 3.5f)
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

                transform.rotation = Quaternion.RotateTowards(transform.rotation, TargetRotation, Time.deltaTime * 180);
            }

            if (ForwardBoost != 0)
            {
                float distance = Vector3.Distance(transform.position, NewMovement.Instance.transform.position);
                float deacceleration = 40;

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

                ForwardBoost = Mathf.MoveTowards(ForwardBoost, 0, Time.deltaTime * deacceleration);
                transform.position += transform.forward * (ForwardBoost * Time.deltaTime) * Machine.eid.totalSpeedModifier;
            }

            Vector3 projectileSpawnPos = (LeftYTracker.transform.position + RightYTracker.transform.position) / 2;
            //projectileSpawnPos.x = transform.position.x;
            ProjectileSpawn.transform.position = projectileSpawnPos + (ProjectileSpawn.transform.forward * 2);
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
                        Debug.Log("reflect from col");
                        rms.Reflect(hit.normal);
                    }

                    Debug.Log("Hit the " + filteredContacts[0].otherCollider.gameObject);
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

        public IEnumerator StompAttack(Vector3 position)
        {
            yield return StartCoroutine(GoVisible());
            Machine.anim.SetBool("Stomp First Frame", true);
            yield return new WaitForSeconds(0.1f);
            yield return StompFall(position);
            Instantiate(ShockwaveAttack, transform.position, Quaternion.identity);
            yield return new WaitForSeconds(1f);
        }

        public IEnumerator StompEntrance(Vector3 position)
        {
            Machine.enabled = true;
            yield return null; // have to wait a frame so Start has the time to get called, this code is aids :3

            yield return StompFall(position);
            Instantiate(ShockwaveIntro, transform.position, Quaternion.identity);

            _hasStomped = true;
        }

        public IEnumerator StompFall(Vector3 position)
        {
            transform.LookAt(position);
            transform.rotation *= Quaternion.Euler(-90, 0, 0);

            Vector3 pointOffset = Vector3.up * 3;
            float rateOfFall = 15f;

            //Debug.Log("1 " + Machine.anim.GetCurrentAnimatorClipInfo(0)[0].clip.name);
            Machine.anim.SetBool("Stomp First Frame", false);
            //Debug.Log("2 " + Machine.anim.GetCurrentAnimatorClipInfo(0)[0].clip.name);
            yield return new WaitForSeconds((1f / 24) * 33); //till it starts falling fr
            //Debug.Log("3 " + Machine.anim.GetCurrentAnimatorClipInfo(0)[0].clip.name);

            bool hasDone = false;
            while (transform.position != position)
            {
                if (transform.position.y < (position + pointOffset).y && !hasDone)
                {
                    Debug.Log($"at {transform.position.y}, reached {(position + pointOffset).y}");
                    Machine.anim.SetTrigger("Stomp Land");
                    hasDone = true;
                }

                rateOfFall = Mathf.MoveTowards(rateOfFall, float.MaxValue, 75 * Time.deltaTime);
                transform.position = Vector3.MoveTowards(transform.position, position, rateOfFall * Time.deltaTime);
                
                yield return null;
            }
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
                Enraged = true;
            }
        }

        public void Deenrage()
        {
            Destroy(CurrentEnragedEffect);

            float oldMaterial = Machine.smr.material.GetFloat("_OpacScale");
            Machine.smr.material = new(DefaultMaterial);
            Machine.smr.material.SetFloat("_OpacScale", oldMaterial);
        }

        public void OnHurt(string origin)
        {
            if (Machine.eid.hitter == "coin" || origin.Contains("Coin.ReflectRevolver")) //this sucks but i cbf to write a transpiler so fuck you
            {
                Enrage();
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

            if (Vector3.Distance(NewMovement.Instance.transform.position, transform.position) <= 25)
            {
                RevState = new RandomMeleeState(this);
                return;
            }

            if (Vector3.Distance(NewMovement.Instance.transform.position, transform.position) > 25)
            {
                bool shouldStomp = false;
                bool didHit = Physics.Raycast(transform.position, Vector3.up, out RaycastHit hit, 1000, LayerMaskDefaults.Get(LMD.Environment));

                if (didHit)
                {
                    shouldStomp = Physics.CheckSphere(hit.point, 3);
                }
                else
                {
                    shouldStomp = true;
                }

                if (shouldStomp)
                {
                    RevState = new StompState(this, hit.distance);
                }
                else
                {
                    RevState = new InvisErraticState(this);
                }

                return;
            }

            if (RevState.GetType() == typeof(PassiveState))
            {
                RevState = new InvisErraticState(this);
                return;
            }

            RevState = new PassiveState(this);
        }

        // called by Machine.GoLimp with a SendMessage, thanks Hakita
        public void Death()
        {
            RevState.End();
            ResetXRotation();
            StartCoroutine(DeathAnimation());
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

            if (invisAnimation)
            {
                Machine.anim.SetTrigger("Turn Invis");
                yield return new WaitForSeconds(0.25f);
            }

            while (Machine.smr.material.GetFloat("_OpacScale") != 0)
            {
                Machine.smr.material.SetFloat("_OpacScale", Mathf.MoveTowards(Machine.smr.material.GetFloat("_OpacScale"), 0, Time.deltaTime * 1.5f));
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
            Instantiate(VisibilitySound, transform.position, transform.rotation);
            while (Machine.smr.material.GetFloat("_OpacScale") != 1)
            {
                Machine.smr.material.SetFloat("_OpacScale", Mathf.MoveTowards(Machine.smr.material.GetFloat("_OpacScale"), 1, Time.deltaTime * 2));
                yield return null;
            }
        }

        public Sprite GetJumpscare()
        {
            return Enraged ? EnragedJumpscarePool[Random.Range(0, EnragedJumpscarePool.Length - 1)] : JumpscarePool[Random.Range(0, JumpscarePool.Length - 1)];
        }

        public IEnumerator Melee1()
        {
            yield return StartCoroutine(GoVisible());
            yield return new WaitForSeconds(0.1f);

            LookAtPlayer = true;

            Machine.anim.SetTrigger("Melee1");
            Machine.parryable = true;
            CreateParryFlash(ParryFlash);
            _trackPlayer = true;

            yield return new WaitForSeconds((1f / 24) * 6); // 24fps, this is 6 frames

            ForwardBoost = 70f;
            if (Machine.parryable)
            {
                HeadSwing.DamageStart();
            }

            yield return new WaitForSeconds((1f / 24) * 17);
            _trackPlayer = false;
            yield return new WaitForSeconds((1f / 24) * 18); // totals to 41 frames

            Machine.parryable = false;
            HeadSwing.DamageStop();
            ResetRotation = true;
            ((RandomMeleeState)RevState).AttackDone = true;

            yield return new WaitForSeconds((1f / 24) * 21); // totals to 62 frames
            LookAtPlayer = false;
            yield return StartCoroutine(GoInvisible());

            ResetXRotation();
            ResetRotation = false;
            ((RandomMeleeState)RevState).AttackDone = false;
        }

        public IEnumerator Melee2()
        {
            yield return StartCoroutine(GoVisible());
            yield return new WaitForSeconds(0.1f);

            LookAtPlayer = true;
            Machine.anim.SetTrigger("Melee2");
            Machine.parryable = false;
            CreateParryFlash(NoParryFlash);

            yield return new WaitForSeconds((1f / 24) * 5); // 5 frames

            _currentPredicted = PlayerTracker.Instance.PredictPlayerPosition(0.5f);
            ForwardBoost = 60f;
            LeftArmSwing.DamageStart();

            yield return new WaitForSeconds((1f / 24) * 5); // totals to 10 frames
            //LookAtPlayer = false;
            yield return new WaitForSeconds((1f / 24) * 16); // totals to 26 frames

            LeftArmSwing.DamageStop();
            LookAtPlayer = false;
            Machine.parryable = true;
            CreateParryFlash(ParryFlash);
            InstantLookAtPlayer();

            yield return new WaitForSeconds((1f / 24) * 5); // totals to 31 frames
            LookAtPlayer = true;
            _currentPredicted = PlayerTracker.Instance.PredictPlayerPosition(0.5f);

            ForwardBoost = 60f;
            if (Machine.parryable)
            {
                RightArmSwing.DamageStart();
            }

            yield return new WaitForSeconds((1f / 24) * 3); // totals to 34 frames
            //LookAtPlayer = false;
            yield return new WaitForSeconds((1f / 24) * 7); // totals to 41 frames

            LookAtPlayer = false;
            Machine.parryable = false;
            RightArmSwing.DamageStop();

            ResetRotation = true;
            ((RandomMeleeState)RevState).AttackDone = true;
            yield return new WaitForSeconds((1f / 24) * 31); // totals to 72 frames
            yield return StartCoroutine(GoInvisible());
            ResetXRotation();
            ResetRotation = false;
            ((RandomMeleeState)RevState).AttackDone = false;
        }

        public IEnumerator Melee3()
        {
            yield return StartCoroutine(GoVisible());
            yield return new WaitForSeconds(0.1f);

            _currentPredicted = PlayerTracker.Instance.PredictPlayerPosition(1f);
            LookAtPlayer = true;
            Machine.anim.SetTrigger("Melee3");

            yield return new WaitForSeconds((1f / 24) * 4); // 4 frames

            Machine.parryable = false;
            CreateParryFlash(NoParryFlash);

            yield return new WaitForSeconds((1f / 24) * 3); // totals to 7 frames
            _trackPlayer = true;

            ForwardBoost = 60f;
            LeftArmSwing.DamageStart();
            RightArmSwing.DamageStart();

            yield return new WaitForSeconds((1f / 24) * 5); // totals to 12 frames

            //LookAtPlayer = false;

            yield return new WaitForSeconds((1f / 24) * 16); // totals to 28 frames

            _trackPlayer = false;
            LookAtPlayer = false;
            LeftArmSwing.DamageStop();
            RightArmSwing.DamageStop();

            ResetRotation = true;
            ((RandomMeleeState)RevState).AttackDone = true;
            yield return new WaitForSeconds((1f / 24) * 24); // totals to 52 frames
            yield return StartCoroutine(GoInvisible());
            ResetXRotation();
            ResetRotation = false;
            ((RandomMeleeState)RevState).AttackDone = false;
        }

        public IEnumerator Stomp()
        {
            yield return StartCoroutine(GoVisible());
        }

        public IEnumerator RangedAttack()
        {
            LookAtPlayer = true;
            yield return StartCoroutine(GoVisible());
            Machine.anim.SetTrigger("Range Attack");
            GameObject decoProjectile = Instantiate(ProjectileDecorative, ProjectileSpawn.transform);
            StartCoroutine(PrepareDecoProjectile(decoProjectile));
            yield return new WaitForSeconds((1f / 24) * 51); //51 frames

            Destroy(decoProjectile);
            GameObject realProjectile = Instantiate(Projectile, ProjectileSpawn.transform.position, ProjectileSpawn.transform.rotation);
            Projectile projectile = realProjectile.GetComponent<Projectile>();
            projectile.target = NewMovement.Instance.transform;
            projectile.speed = 10f * Machine.eid.totalSpeedModifier;
            projectile.damage *= Machine.eid.totalDamageModifier;
            LookAtPlayer = false;
            ((ProjectileAttackState)RevState).AttackDone = true;

            ResetRotation = true;
            yield return new WaitForSeconds((1f / 24) * 34); // 85
            yield return StartCoroutine(GoInvisible(0, true));
            ResetXRotation();
            ResetRotation = false;
            ((ProjectileAttackState)RevState).AttackDone = false;
        }

        public IEnumerator PrepareDecoProjectile(GameObject projectile)
        {
            //24 x 3 is 72. takes 3 seconds to prepare
            float timeElapsed = 0;
            AudioSource source = projectile.GetComponent<AudioSource>();
            float startVol = source.volume;
            Vector3 startScale = projectile.transform.localScale;
            
            while (timeElapsed < 3)
            {
                source.volume = startVol * (timeElapsed / 3);
                projectile.transform.localScale = startScale * (timeElapsed / 3);
                timeElapsed += Time.deltaTime;
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

            yield return new WaitForSeconds((1f / 24) * 75); // i will die before i use anim events

            Machine.anim.enabled = false;
            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
    }
}
