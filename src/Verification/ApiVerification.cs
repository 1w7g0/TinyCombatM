// Compile-time verification of Tiny Combat Arena game API accessibility.
// If this file compiles, these APIs can be accessed directly without reflection.
// This file is NOT intended to be executed — only compiled.
//
// Convention:
//   ✅ = compiles, direct access confirmed
//   ❌ = does not compile, reflection required (commented out with explanation)

#pragma warning disable CS0219  // Variable assigned but never used
#pragma warning disable CS0168  // Variable declared but never used
#pragma warning disable CS8321  // Local function declared but never used
#pragma warning disable CS0414  // Field assigned but never used

using System;
using System.Collections.Generic;
using Falcon;
using Falcon.Targeting;
using Falcon.Weapons;
using Falcon.Damage;
using Falcon.World;
using Falcon.UniversalAircraft;
using Falcon.Stores;
using Falcon.Factions;
using Falcon.Utilities;
using UnityEngine;

namespace TCAMultiplayer.Verification
{
    /// <summary>
    /// Compile-time verification of game API accessibility.
    /// If this file compiles, these APIs can be accessed directly without reflection.
    /// </summary>
    internal static class ApiVerification
    {
        // =====================================================================
        // GameDataAircraft (Falcon namespace) — public static class
        // =====================================================================
        private static void VerifyGameDataAircraft()
        {
            // ✅ GetByName(string) → UniAircraftData
            UniAircraftData data = GameDataAircraft.GetByName("F-16C");

            // ✅ SpawnAircraft(string, string, JFaction, PilotSkill, Vector3, Quaternion, bool) → UniAircraft
            JFaction faction = null;
            UniAircraft aircraft = GameDataAircraft.SpawnAircraft("test", "F-16C", faction, PilotSkill.Average, Vector3.zero, Quaternion.identity, true);

            // ✅ HasGun(string) → bool
            bool hasGun = GameDataAircraft.HasGun("F-16C");

            // ✅ GetListOfAllAircraft() → List<string>
            List<string> allAircraft = GameDataAircraft.GetListOfAllAircraft();
        }

        // =====================================================================
        // GameDataStores (Falcon namespace) — public static class
        // =====================================================================
        private static void VerifyGameDataStores()
        {
            // ✅ SpawnStore(string) → Store
            Store store = GameDataStores.SpawnStore("AIM-9M");

            // ✅ HasStore(string) → bool
            bool hasStore = GameDataStores.HasStore("AIM-9M");
        }

        // =====================================================================
        // GameDataBullets (Falcon namespace) — public static class
        // =====================================================================
        private static void VerifyGameDataBullets()
        {
            // ✅ GetByName(string) → BulletData
            BulletData bullet = GameDataBullets.GetByName("20mm");
        }

        // =====================================================================
        // GameDataGuns (Falcon namespace) — public static class
        // =====================================================================
        private static void VerifyGameDataGuns()
        {
            // ✅ GetByName(string) → GunData
            GunData gun = GameDataGuns.GetByName("M61A1");

            // ✅ HasGun(string) → bool
            bool hasGun = GameDataGuns.HasGun("M61A1");
        }

        // =====================================================================
        // Target (Falcon.Targeting) — public class : MonoBehaviour
        // =====================================================================
        private static void VerifyTarget()
        {
            Target t = null;

            // ✅ Position — public get (computed property)
            Vector3 pos = t.Position;

            // ✅ Velocity — public get, private set (read-only from outside)
            Vector3 vel = t.Velocity;
            // ❌ t.Velocity = Vector3.zero; // CS0200: private setter — REFLECTION REQUIRED to set

            // ✅ Faction — public get, private set (read-only from outside)
            string faction = t.Faction;
            // ❌ t.Faction = "USAF"; // CS0200: private setter — use SetFaction() instead

            // ✅ Coalition — public get, private set (read-only from outside)
            Coalition coal = t.Coalition;

            // ✅ IsTargetable — public get (computed property)
            bool targetable = t.IsTargetable;

            // ✅ IsDestroyed — public FIELD (not property!)
            bool destroyed = t.IsDestroyed;
            t.IsDestroyed = true; // ✅ writable — it's a public field

            // ✅ TargetType — public FIELD
            TargetType tt = t.TargetType;
            t.TargetType = TargetType.Fighter; // ✅ writable

            // ✅ Signature — public FIELD
            Signature sig = t.Signature;

            // ✅ SetFaction(JFaction) — public method
            JFaction jf = null;
            t.SetFaction(jf);

            // ✅ Additional public fields
            bool isImportant = t.IsImportant;
            bool isStatic = t.IsStatic;
            Coalition defaultCoal = t.DefaultCoalition;
            string dataName = t.DataName;
            bool isObjective = t.IsObjective;
            bool isCriticalHP = t.IsCriticalHP;
            Vector3 size = t.Size;

            // ✅ Transform and Rigidbody properties (public get, private set)
            Transform tr = t.Transform;
            Rigidbody rb = t.Rigidbody;

            // ✅ Static methods
            bool isAir = Target.IsAirTarget(TargetType.Fighter);
            bool isSurface = Target.IsSurfaceTarget(TargetType.SAM);
        }

        // =====================================================================
        // TargetManagement (Falcon.Targeting) — public static class
        // =====================================================================
        private static void VerifyTargetManagement()
        {
            // ✅ AllTargets — public static field
            List<Target> all = TargetManagement.AllTargets;

            // ✅ RegisterTarget / UnregisterTarget — public static methods
            Target t = null;
            TargetManagement.RegisterTarget(t);
            TargetManagement.UnregisterTarget(t);

            // ✅ OnTargetAdded — public static Action<Target> FIELD (not event!)
            // Can use += directly on delegate fields
            TargetManagement.OnTargetAdded += (target) => { };
            TargetManagement.OnTargetAdded -= (target) => { };
            // ✅ Can also assign directly (it's a field, not an event)
            Action<Target> existingAdded = TargetManagement.OnTargetAdded;

            // ✅ OnTargetRemoved — public static Action<Target> FIELD (not event!)
            TargetManagement.OnTargetRemoved += (target) => { };
            Action<Target> existingRemoved = TargetManagement.OnTargetRemoved;

            // ✅ TargetsByType — public static field
            Dictionary<TargetType, List<Target>> byType = TargetManagement.TargetsByType;

            // ✅ TargetsByFaction — public static field
            Dictionary<string, List<Target>> byFaction = TargetManagement.TargetsByFaction;

            // ✅ TargetsByCoalition — public static field
            Dictionary<Coalition, List<Target>> byCoalition = TargetManagement.TargetsByCoalition;
        }

        // =====================================================================
        // Radar (Falcon.Targeting) — public class
        // =====================================================================
        private static void VerifyRadar()
        {
            Radar r = null;

            // ✅ ActiveRadars — public static field
            List<Radar> active = Radar.ActiveRadars;

            // ✅ LockedTarget — public get, private set
            Target locked = r.LockedTarget;
            // ❌ r.LockedTarget = null; // CS0200: private setter — use LockTarget()/UnlockTarget()

            // ✅ OwnTarget — public get, private set
            Target own = r.OwnTarget;

            // ✅ IsActive — public get, private set
            bool isActive = r.IsActive;
            // ❌ r.IsActive = true; // CS0200: private setter — use SetActive()

            // ✅ Position — public get (computed from Transform)
            Vector3 pos = r.Position;

            // ✅ Forward — public get (computed from Transform)
            Vector3 fwd = r.Forward;

            // ✅ LockTarget(Target, bool) — public method
            bool lockResult = r.LockTarget(null, true);

            // ✅ UnlockTarget() — public method
            r.UnlockTarget();

            // ✅ SetActive(bool) — public method (wraps Activate/Deactivate)
            r.SetActive(true);

            // ✅ ActivateRadar() / DeactivateRadar() — public methods
            r.ActivateRadar();
            r.DeactivateRadar();

            // ✅ Data — public get, private set → returns RadarData
            RadarData data = r.Data;

            // ✅ RadarData fields — all public
            float fov = data.FieldOfView;
            float effectiveRange = data.EffectiveRange;
            float detectability = data.Detectability;
            float maxRange = data.MaxRange;

            // ✅ DetectedTargets — public field
            HashSet<Target> detected = r.DetectedTargets;

            // ✅ ScanForTargets — public method
            r.ScanForTargets();

            // ✅ Static dictionaries — public
            Dictionary<Target, HashSet<Radar>> tracking = Radar.RadarTracking;
            Dictionary<Target, HashSet<Radar>> radarLocked = Radar.RadarLocked;
        }

        // =====================================================================
        // ThreatWarning (Falcon.Targeting) — public class
        // =====================================================================
        private static void VerifyThreatWarning()
        {
            ThreatWarning tw = null;

            // ✅ Threats — public field (HashSet<Radar>)
            HashSet<Radar> threats = tw.Threats;

            // ✅ Missiles — public field (HashSet<Munition>)
            HashSet<Munition> missiles = tw.Missiles;

            // ✅ IsActive — public field
            bool isActive = tw.IsActive;
            tw.IsActive = false; // ✅ writable — it's a public field

            // ✅ OwnRadar — public field
            Radar ownRadar = tw.OwnRadar;

            // ✅ Refresh() — public method
            tw.Refresh();

            // ✅ GetLockedThreats(ref List<Radar>) — public method
            List<Radar> lockedBy = new List<Radar>();
            bool hasLocked = tw.GetLockedThreats(ref lockedBy);

            // ✅ IsMissileAThreat(Munition, Target) — public static method
            bool isThreat = ThreatWarning.IsMissileAThreat(null, null);

            // ✅ WasNewThreatDetectedLastRefresh — public field
            bool wasNew = tw.WasNewThreatDetectedLastRefresh;
        }

        // =====================================================================
        // Munition (Falcon.Stores) — public class : Store
        // =====================================================================
        private static void VerifyMunition()
        {
            Munition m = null;

            // ✅ LaunchedMissiles — public static field
            List<Munition> missiles = Munition.LaunchedMissiles;

            // ✅ LaunchedMunitions — public static field
            List<Munition> munitions = Munition.LaunchedMunitions;

            // ✅ HasExploded — public get, private set
            bool exploded = m.HasExploded;

            // ✅ Target — public FIELD
            Target target = m.Target;
            m.Target = null; // ✅ writable — it's a public field

            // ✅ Seeker — public get (property wrapping private field)
            Seeker seeker = m.Seeker;

            // ✅ HasSeeker — public get
            bool hasSeeker = m.HasSeeker;

            // ✅ GetUnityPosition() — public method
            Vector3 pos = m.GetUnityPosition();

            // ✅ SeekerSignature — public get
            GuidanceType guidanceType = m.SeekerSignature;

            // ✅ Ownship — public field
            Target ownship = m.Ownship;

            // ✅ IsLaunched — public get, private set
            bool isLaunched = m.IsLaunched;

            // ✅ OnDestroyed — public field (Action, not event)
            Action onDestroyed = m.OnDestroyed;
        }

        // =====================================================================
        // Seeker (Falcon.Stores) — public class
        // =====================================================================
        private static void VerifySeeker()
        {
            Seeker s = null;

            // ✅ IsTracking — public get, private set
            bool tracking = s.IsTracking;

            // ✅ IsSpoofed — public get, private set
            bool spoofed = s.IsSpoofed;

            // ✅ Target — public get, private set
            Target target = s.Target;

            // ✅ Data — public field (GuidanceProperties)
            GuidanceProperties data = s.Data;

            // ✅ HasTarget — public get
            bool hasTarget = s.HasTarget;
        }

        // =====================================================================
        // Bullet2Manager (Falcon.Weapons) — public class : MonoBehaviour
        // =====================================================================
        private static void VerifyBullet2Manager()
        {
            // ✅ Instance — public static field
            Bullet2Manager instance = Bullet2Manager.Instance;

            // ✅ FireBullet — public method, exact parameter types match
            // Signature: FireBullet(Target, BulletData, string, Vector3, Vector3, IEnumerable<Collider>, IEnumerable<Rigidbody>) → Bullet2
            Target firedFrom = null;
            BulletData data = null;
            IEnumerable<Collider> ignoredColliders = null;
            IEnumerable<Rigidbody> ignoredRbs = null;
            Bullet2 bullet = instance.FireBullet(firedFrom, data, "TestGun", Vector3.zero, Vector3.forward, ignoredColliders, ignoredRbs);

            // ✅ SpawnEffect(string, Vector3, Quaternion) — public method
            instance.SpawnEffect("HitMG", Vector3.zero, Quaternion.identity);
        }

        // =====================================================================
        // Gun2 (Falcon.Weapons) — public class
        // =====================================================================
        private static void VerifyGun2()
        {
            Gun2 g = null;

            // ✅ IsFiring — public FIELD
            bool firing = g.IsFiring;
            g.IsFiring = true; // ✅ writable

            // ✅ GunData — public FIELD
            GunData gunData = g.GunData;

            // ✅ AmmoBelt — public FIELD
            AmmoBelt belt = g.AmmoBelt;

            // ✅ Barrels — public FIELD
            List<Barrel2> barrels = g.Barrels;

            // ✅ OwnTarget — public FIELD
            Target ownTarget = g.OwnTarget;

            // ✅ Ammo — public FIELD
            int ammo = g.Ammo;
            g.Ammo = 100; // ✅ writable
        }

        // =====================================================================
        // GunData (Falcon.Weapons) — public class : LoadableData
        // =====================================================================
        private static void VerifyGunData()
        {
            GunData gd = null;

            // ✅ DisplayName — public FIELD
            string displayName = gd.DisplayName;

            // ✅ Bullet — public FIELD
            string bulletName = gd.Bullet;

            // ✅ AmmoCount — public FIELD
            int ammoCount = gd.AmmoCount;

            // ✅ Gun — public FIELD (GunFireData)
            GunFireData gunFire = gd.Gun;

            // ✅ GunFireData fields — all public
            float muzzleVel = gunFire.MuzzleVelocity;
            float deviation = gunFire.Deviation;
            float fireDelay = gunFire.FireDelay;
            int burstLength = gunFire.BurstLength;
            float burstDelay = gunFire.BurstDelay;
            bool sequential = gunFire.UseSequentialFiring;

            // ✅ GetDisplayName() — public method
            string display = gd.GetDisplayName();
        }

        // =====================================================================
        // BulletData (Falcon.Weapons) — public class : LoadableData
        // =====================================================================
        private static void VerifyBulletData()
        {
            BulletData bd = null;

            // ✅ Type — public FIELD (BulletType enum)
            BulletType type = bd.Type;

            // ✅ Other public fields
            float ttl = bd.TimeToLive;
            int damage = bd.ImpactDamage;
            int penetration = bd.ImpactPenetration;
        }

        // =====================================================================
        // AmmoBelt (Falcon.Weapons) — public class
        // =====================================================================
        private static void VerifyAmmoBelt()
        {
            AmmoBelt ab = null;

            // ✅ GetNextBullet() — public method
            BulletData next = ab.GetNextBullet();

            // ✅ PeekNextBullet() — public method
            BulletData peek = ab.PeekNextBullet();

            // ✅ AverageTimeToLive — public readonly field
            float avgTTL = ab.AverageTimeToLive;

            // ✅ AverageGravity — public readonly field
            float avgGrav = ab.AverageGravity;
        }

        // =====================================================================
        // Damageable (Falcon.Damage) — public class : MonoBehaviour
        // =====================================================================
        private static void VerifyDamageable()
        {
            Damageable d = null;

            // ✅ HitPoints — public get/set
            int hp = d.HitPoints;
            d.HitPoints = 100; // ✅ public setter

            // ✅ MaxHitpoints — public get/set
            int maxHp = d.MaxHitpoints;
            d.MaxHitpoints = 200; // ✅ public setter

            // ✅ IsDestroyed — public get, private set
            bool destroyed = d.IsDestroyed;
            // ❌ d.IsDestroyed = true; // CS0200: private setter — REFLECTION REQUIRED to set

            // ✅ IsAlive — public get (computed from IsDestroyed)
            bool alive = d.IsAlive;

            // ✅ IsDamaged — public get (computed)
            bool damaged = d.IsDamaged;

            // ✅ Target — public get, private set
            Target target = d.Target;
            // ❌ d.Target = null; // CS0200: private setter — set in Awake via GetComponentInParent

            // ✅ MostRecentDamage — public get, private set
            DamageSource recent = d.MostRecentDamage;

            // ✅ ApplyDamageFromImpact(DamageSource) — public method
            d.ApplyDamageFromImpact(default(DamageSource));

            // ✅ ApplyDamageFromExplosion(DamageSource) — public method
            d.ApplyDamageFromExplosion(default(DamageSource));

            // ✅ OnDestroyedAction — public EVENT (has add/remove)
            d.OnDestroyedAction += (DestroyedEvent evt) => { };
            d.OnDestroyedAction -= (DestroyedEvent evt) => { };

            // ✅ OnAnythingDestroyed — public static EVENT (has add/remove)
            Damageable.OnAnythingDestroyed += (DestroyedEvent evt) => { };
            Damageable.OnAnythingDestroyed -= (DestroyedEvent evt) => { };

            // ✅ Armor — public field
            int armor = d.Armor;

            // ✅ IsInvincible — public field
            bool invincible = d.IsInvincible;
        }

        // =====================================================================
        // DamageSource (Falcon.Damage) — public struct
        // =====================================================================
        private static void VerifyDamageSource()
        {
            // ✅ Constructor with 9 parameters — all accessible
            // DamageSource(int damage, int penetration, int critHitChance, int maxCritHits,
            //              Target source, Collider hitCollider, Vector3 hitPos,
            //              bool isCausedByWeapon, string weapon)
            Target source = null;
            Collider hitCol = null;
            DamageSource ds = new DamageSource(
                100,        // damage
                50,         // penetration
                30,         // criticalHitChance
                2,          // maxCriticalHits
                source,     // source Target
                hitCol,     // hitCollider
                Vector3.zero, // hitPosition
                true,       // isCausedByWeapon
                "M61A1"     // weapon name
            );

            // ✅ Copy constructor
            DamageSource copy = new DamageSource(ds);

            // ✅ All public fields
            int dmg = ds.Damage;
            int pen = ds.Penetration;
            int crit = ds.CriticalHitChance;
            int maxCrit = ds.MaxCriticalHits;
            Target src = ds.SourceTarget;
            Collider col = ds.HitCollider;
            Vector3 hitPos = ds.HitPosition;
            float dmgTime = ds.DamageTime;
            string weapon = ds.Weapon;
            bool causedByWeapon = ds.IsCausedByWeapon;

            // ✅ All fields writable
            ds.Damage = 200;
            ds.Penetration = 100;
            ds.SourceTarget = null;
            ds.HitPosition = Vector3.one;
            ds.Weapon = "AIM-9M";
            ds.IsCausedByWeapon = false;
        }

        // =====================================================================
        // Explosion (Falcon.Damage) — public struct
        // =====================================================================
        private static void VerifyExplosion()
        {
            // ✅ Constructor(Vector3 position, float force, float radius)
            Explosion exp = new Explosion(Vector3.zero, 1000f, 50f);

            // ✅ Trigger(Explosion, DamageSource) — public static async void
            Explosion.Trigger(exp, default(DamageSource));

            // ✅ OnExplosion — public static EVENT (confirmed: has (add)/(remove) tokens)
            // This IS a C# event, so += works, direct invocation does NOT
            Explosion.OnExplosion += (ExplosionEventParams p) => { };
            Explosion.OnExplosion -= (ExplosionEventParams p) => { };
            // ❌ Explosion.OnExplosion = null; // CS0070: cannot assign to event — it's an event, not a field
            // ❌ Explosion.OnExplosion(params); // CS0070: cannot invoke event outside declaring type

            // ✅ Public fields
            Vector3 pos = exp.Position;
            float force = exp.Force;
            float radius = exp.Radius;

            // ✅ ExplosionEventParams — public struct in Falcon.Damage
            ExplosionEventParams evtParams = new ExplosionEventParams(exp, default(DamageSource));
            Explosion evtExplosion = evtParams.Explosion;
            DamageSource evtDamage = evtParams.Damage;
        }

        // =====================================================================
        // FloatingOrigin (Falcon.World) — public class : MonoBehaviour
        // =====================================================================
        private static void VerifyFloatingOrigin()
        {
            // ✅ TotalOffset — public static get, private set
            Vector3 offset = FloatingOrigin.TotalOffset;
            // ❌ FloatingOrigin.TotalOffset = Vector3.zero; // CS0200: private setter — REFLECTION REQUIRED

            // ✅ Instance — public static get, private set
            FloatingOrigin instance = FloatingOrigin.Instance;

            // ✅ OnOriginShiftStart — public static Action<Vector3> FIELD (NOT an event!)
            // Confirmed from decompiled: no (add)/(remove) tokens, plain field
            FloatingOrigin.OnOriginShiftStart += (Vector3 v) => { };
            FloatingOrigin.OnOriginShiftStart -= (Vector3 v) => { };
            // ✅ Can also read/assign directly (it's a field)
            Action<Vector3> existingStart = FloatingOrigin.OnOriginShiftStart;
            FloatingOrigin.OnOriginShiftStart = null; // ✅ direct assignment works on fields

            // ✅ OnOriginShiftFinished — public static Action<Vector3> FIELD (NOT an event!)
            FloatingOrigin.OnOriginShiftFinished += (Vector3 v) => { };
            FloatingOrigin.OnOriginShiftFinished -= (Vector3 v) => { };
            Action<Vector3> existingFinished = FloatingOrigin.OnOriginShiftFinished;
            FloatingOrigin.OnOriginShiftFinished = null; // ✅ direct assignment works

            // ✅ Blacklist — public static field
            List<GameObject> blacklist = FloatingOrigin.Blacklist;
        }

        // =====================================================================
        // UniAircraft (Falcon.UniversalAircraft) — public class : MonoBehaviour
        // =====================================================================
        private static void VerifyUniAircraft()
        {
            UniAircraft ac = null;

            // ✅ HasBeenDestroyed — public get, private set
            bool destroyed = ac.HasBeenDestroyed;

            // ✅ Radar — public get, private set
            Radar radar = ac.Radar;

            // ✅ ThreatWarning — public get, private set
            ThreatWarning tw = ac.ThreatWarning;

            // ✅ Engines — public get, private set
            List<UniEngine> engines = ac.Engines;

            // ✅ Fuel — public get, private set
            UniFuelTank fuel = ac.Fuel;

            // ✅ Flaps — public get, private set
            UniFlaps flaps = ac.Flaps;

            // ✅ Countermeasures — public get, private set
            CountermeasureLauncher cm = ac.Countermeasures;

            // ✅ FlightDamage — public get, private set
            UniAircraftDamage flightDmg = ac.FlightDamage;

            // ✅ OnAircraftDestroyed — public EVENT (confirmed: has (add)/(remove) tokens)
            ac.OnAircraftDestroyed += (UniAircraft a) => { };
            ac.OnAircraftDestroyed -= (UniAircraft a) => { };
            // ❌ ac.OnAircraftDestroyed = null; // CS0070: cannot assign to event
            // ❌ ac.OnAircraftDestroyed(ac); // CS0070: cannot invoke event outside declaring type

            // ✅ UniPilot — public get, private set
            UniPilot pilot = ac.UniPilot;

            // ✅ IsInitialized — public get, private set
            bool init = ac.IsInitialized;

            // ✅ IsFrozen — public get, private set
            bool frozen = ac.IsFrozen;
        }

        // =====================================================================
        // UniPilot (Falcon.UniversalAircraft) — public class
        // =====================================================================
        private static void VerifyUniPilot()
        {
            UniPilot pilot = null;

            // ✅ Ownship — public get, private set
            UniAircraft ownship = pilot.Ownship;

            // ✅ IsInFlight — public get
            bool inFlight = pilot.IsInFlight;

            // ✅ IsFlightLead — public get
            bool isLead = pilot.IsFlightLead;

            // ✅ ActiveBehaviorType — public get
            BehaviorType behaviorType = pilot.ActiveBehaviorType;
        }

        // =====================================================================
        // Environment (Falcon.World) — public class : SingletonMonobehaviour<Environment>
        // =====================================================================
        private static void VerifyEnvironment()
        {
            // ✅ Type accessible — confirm the type compiles
            Falcon.World.Environment env = null;

            // ✅ Access via singleton — SingletonMonobehaviour<T>.Instance
            Falcon.World.Environment envInstance = SingletonMonobehaviour<Falcon.World.Environment>.Instance;

            // ❌ TimeOfDaySeconds — PRIVATE field (with [SerializeField])
            // Cannot read directly: float tod = env.TimeOfDaySeconds; // CS0122: inaccessible
            // REFLECTION REQUIRED to read the current time of day
            // Workaround: env.TODTimespan is public get → can compute seconds from TimeSpan

            // ✅ TODTimespan — public get, private set (can derive seconds from this)
            TimeSpan timespan = env.TODTimespan;

            // ✅ SetTimeOfDaySeconds(float) — public method (can SET time of day)
            env.SetTimeOfDaySeconds(36000f);

            // ✅ WindVelocityMS — public get, private set
            Vector3 wind = env.WindVelocityMS;
            // ❌ env.WindVelocityMS = Vector3.zero; // CS0200: private setter

            // ✅ WindSpeedMS — public field
            float windSpeed = env.WindSpeedMS;

            // ✅ WindHeading — public field
            float windHeading = env.WindHeading;
        }

        // =====================================================================
        // Supporting types verification
        // =====================================================================
        private static void VerifySupportingTypes()
        {
            // ✅ Coalition enum (Falcon.Factions)
            Coalition c = Coalition.Blue;
            Coalition r = Coalition.Red;
            Coalition n = Coalition.Neutral;

            // ✅ TargetType enum (Falcon.Targeting)
            TargetType fighter = TargetType.Fighter;
            TargetType sam = TargetType.SAM;

            // ✅ BulletType enum (Falcon.Weapons)
            BulletType mg = BulletType.Machinegun;
            BulletType aircraft = BulletType.Aircraft;

            // ✅ GuidanceType enum (Falcon.Stores)
            GuidanceType ir = GuidanceType.Infrared;
            GuidanceType ar = GuidanceType.ActiveRadar;

            // ✅ DestroyedEvent struct (Falcon.Damage)
            DestroyedEvent de = default;
            Damageable destroyed = de.Destroyed;
            DamageSource dmgSrc = de.DamageSource;

            // ✅ ExplosionEventParams struct (Falcon.Damage)
            ExplosionEventParams ep = default;
            Explosion explosion = ep.Explosion;
            DamageSource epDmg = ep.Damage;

            // ✅ PilotSkill enum (Falcon.UniversalAircraft)
            PilotSkill skill = PilotSkill.Average;

            // ✅ GuidanceProperties (Falcon.Stores)
            GuidanceProperties gp = null;
        }
    }
}

#pragma warning restore CS0219
#pragma warning restore CS0168
#pragma warning restore CS8321
#pragma warning restore CS0414
