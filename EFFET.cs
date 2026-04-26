// LimbusInjector (ver.1.6.0)
// F8 키로 패널 토글
// 탭 1 — Buff: BUFF_UNIQUE_KEYWORD 열거형 기반 버프 주입 (AddBuff_NonGiver, 리플렉션)
// 탭 2 — Ability: SYSTEM_ABILITY_KEYWORD 기반 주입 (AddAbilityThisRound, 리플렉션)
// REPL 검증 결과 반영:
// - GetUniqueName() 사용 (GetName()은 "Unknown Unknown" 반환)
// - GetModelList / AddBuff_NonGiver / AddAbilityThisRound 리플렉션 우회
// - OverlapAbility(string)은 실제 어빌리티를 추가하지 않아 폐기

using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace LimbusInjector
{
    [BepInPlugin("com.mod.limbusinjector", "LimbusInjector", "1.6.0")]
    public class LimbusInjectorPlugin : BasePlugin
    {
        internal static new ManualLogSource? Log;

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("LimbusInjector v1.6.0 loaded | F8 키로 패널 토글");
            AddComponent<InjectorUI>();
        }
    }

    public class InjectorUI : MonoBehaviour
    {
        // ── 상태 ─────────────────────────────────────────────────────────
        private bool _showPanel = false;
        private int  _activeTab = 0; // 0=Buff 1=Ability

        private string _status = "";

        // 유닛 선택 (object로 보관 — BattleUnitModel 직접 참조 시 IL2CPP 로드 오류 방지)
        private UNIT_FACTION _faction      = UNIT_FACTION.PLAYER;
        private object?      _selectedUnit = null;
        private int          _selectedInstanceID = -1;
        private string       _selectedName = "";

        // Buff 탭
        // (BUFF_UNIQUE_KEYWORD 이름, 한국어 이름, buffType, buffClass)
        private string _buffSearch  = "";
        private int    _buffPage    = 0;
        private int    _buffTypeIdx = 0; // 0=All 1=Positive 2=Negative 3=SinBuff 4=None
        private string _stackInput  = "1";
        private string _turnInput   = "3";

        private readonly string[] _buffTypeLabels = { "All", "버프", "디버프", "죄악", "기타" };

        private readonly (string id, string kr, string buffType, string buffClass)[] _allBuffKeywords = {
            ("Enhancement", "공격 위력 증가", "Positive", "None"),
            ("Agility", "신속", "Positive", "None"),
            ("Endurance", "수비 위력 증가", "None", "None"),
            ("Reduction", "공격 위력 감소", "Negative", "None"),
            ("Binding", "속박", "Negative", "None"),
            ("Vulnerable", "취약", "Negative", "None"),
            ("Charge", "충전", "None", "SinBuff"),
            ("EgoErode", "E.G.O침식", "None", "None"),
            ("Overwhelm_LowMorale", "압도 (사기저하)", "None", "None"),
            ("Overwhelm_Panic", "압도", "None", "None"),
            ("Daunted_LowMorale", "위축 (사기저하)", "None", "None"),
            ("Daunted_Panic", "위축", "None", "None"),
            ("Anger_LowMorale", "분함", "None", "None"),
            ("Anger_Panic", "분함", "None", "None"),
            ("Run_LowMorale", "도주", "None", "None"),
            ("Run_Panic", "압도", "None", "None"),
            ("ResultEnhancement", "위력 증가", "Positive", "None"),
            ("Protection", "보호", "Positive", "None"),
            ("AttackDmgUp", "피해량 증가", "Positive", "None"),
            ("DefenseUp", "방어 레벨 증가", "Positive", "VolatileBuff"),
            ("PlusCoinValueUp", "더하기 코인 강화", "Positive", "None"),
            ("MinusCoinValueUp", "빼기 코인 강화", "Positive", "None"),
            ("Disarming", "수비 위력 감소", "Negative", "None"),
            ("ResultReduction", "위력 감소", "Negative", "None"),
            ("AttackDmgDown", "피해량 감소", "Negative", "None"),
            ("DefenseDown", "방어 레벨 감소", "Negative", "None"),
            ("Paralysis", "마비", "Negative", "None"),
            ("ElectricShock", "감전", "Negative", "None"),
            ("PlusCoinValueDown", "더하기 코인 약화", "Negative", "None"),
            ("MinusCoinValueDown", "빼기 코인 약화", "Negative", "None"),
            ("Inactible", "행동 불가", "Negative", "None"),
            ("BeingGolden", "황금빛", "None", "None"),
            ("Operation", "시술", "None", "None"),
            ("BabayagaTimeLimit", "다가오는 바바야가", "None", "None"),
            ("Enrage", "광란", "None", "None"),
            ("Aggressive", "본능", "None", "None"),
            ("Nail", "N사의 못", "None", "None"),
            ("LittleCourage", "어느 표지", "None", "None"),
            ("Combustion", "화상", "None", "SinBuff"),
            ("Laceration", "출혈", "None", "SinBuff"),
            ("Vibration", "진동", "None", "SinBuff"),
            ("Burst", "파열", "None", "SinBuff"),
            ("Sinking", "침잠", "None", "SinBuff"),
            ("Breath", "호흡", "None", "SinBuff"),
            ("BloodPocket", "피주머니", "None", "None"),
            ("WaterPocket", "물주머니", "None", "None"),
            ("DogThunder", "번개", "None", "None"),
            ("AttackUp", "공격 레벨 증가", "None", "None"),
            ("AttackDown", "공격 레벨 감소", "None", "None"),
            ("SlashResistUp", "참격 내성 강화", "None", "None"),
            ("PenetrateResistUp", "관통 내성 강화", "None", "None"),
            ("HitResistUp", "타격 내성 강화", "None", "None"),
            ("SlashDamageUp", "참격 피해량 증가", "None", "None"),
            ("PenetrateDamageUp", "관통 피해량 증가", "None", "None"),
            ("HitDamageUp", "타격 피해량 증가", "None", "None"),
            ("Bullet", "탄환", "None", "None"),
            ("SlashResistDown", "참격 내성 약화", "None", "None"),
            ("PenetrateResistDown", "관통 내성 약화", "None", "None"),
            ("HitResistDown", "타격 내성 약화", "None", "None"),
            ("SlashDamageDown", "참격 피해량 감소", "None", "None"),
            ("PenetrateDamageDown", "관통 피해량 감소", "None", "None"),
            ("HitDamageDown", "타격 피해량 감소", "None", "None"),
            ("CrimsonResistDown", "분노 내성 약화", "None", "None"),
            ("ScarletResistDown", "색욕 내성 약화", "None", "None"),
            ("AmberResistDown", "나태 내성 약화", "None", "None"),
            ("ShamrockResistDown", "탐식 내성 약화", "None", "None"),
            ("AzureResistDown", "우울 내성 약화", "None", "None"),
            ("IndigoResistDown", "오만 내성 약화", "None", "None"),
            ("VioletResistDown", "질투 내성 약화", "None", "None"),
            ("CrimsonResistUp", "분노 내성 강화", "None", "None"),
            ("ScarletResistUp", "색욕 내성 강화", "None", "None"),
            ("AmberResistUp", "나태 내성 강화", "None", "None"),
            ("ShamrockResistUp", "탐식 내성 강화", "None", "None"),
            ("AzureResistUp", "우울 내성 강화", "None", "None"),
            ("IndigoResistUp", "오만 내성 강화", "None", "None"),
            ("VioletResistUp", "질투 내성 강화", "None", "None"),
            ("CrimsonDamageDown", "분노 피해량 감소", "None", "None"),
            ("ScarletDamageDown", "색욕 피해량 감소", "None", "None"),
            ("AmberDamageDown", "나태 피해량 감소", "None", "None"),
            ("ShamrockDamageDown", "탐식 피해량 감소", "None", "None"),
            ("AzureDamageDown", "우울 피해량 감소", "None", "None"),
            ("IndigoDamageDown", "오만 피해량 감소", "None", "None"),
            ("VioletDamageDown", "질투 피해량 감소", "None", "None"),
            ("CrimsonDamageUp", "분노 피해량 증가", "None", "None"),
            ("ScarletDamageUp", "색욕 피해량 증가", "None", "None"),
            ("AmberDamageUp", "나태 피해량 증가", "None", "None"),
            ("ShamrockDamageUp", "탐식 피해량 증가", "None", "None"),
            ("AzureDamageUp", "우울 피해량 증가", "None", "None"),
            ("IndigoDamageUp", "오만 피해량 증가", "None", "None"),
            ("VioletDamageUp", "질투 피해량 증가", "None", "None"),
            ("Curse", "저주", "None", "None"),
            ("Muckworm", "구더기", "None", "None"),
            ("PinkRibbon_Ishmael", "분홍 리본", "None", "None"),
            ("AttackDmgUp_Weak", "약점 공격 시 피해 증가", "None", "None"),
            ("Meursault_Last_Remodeling", "마지막 개조", "None", "None"),
            ("Assemble", "광신", "None", "None"),
            ("ReinforcedAssemble", "크로머의 휘파람", "None", "None"),
            ("Greedy", "탐욕", "None", "None"),
            ("Decay", "썩어가는 피부", "None", "None"),
            ("Poison", "독", "None", "None"),
            ("Choice_90103301", "바둥거림", "None", "None"),
            ("Choice_901001", "불번짐", "None", "None"),
            ("Choice_901007", "나긋한 배웅", "None", "None"),
            ("Choice_901009", "명순응", "None", "None"),
            ("Choice_901010", "비워짐", "None", "None"),
            ("Choice_901019", "오물", "None", "None"),
            ("Choice_90103402", "후련함", "None", "None"),
            ("Choice_1030301", "선택지 효과", "None", "None"),
            ("Choice_1030501", "선택지 효과", "None", "None"),
            ("Choice_1031001", "선택지 효과", "None", "None"),
            ("Whistle_Fear", "휘파람의 공포", "None", "None"),
            ("Whistle_Courage", "공포를 이기는 용기", "None", "None"),
            ("Cromer_Target", "쥐는 자의 주시", "None", "None"),
            ("Cromer_Boredom", "고통을 받아들여!", "None", "None"),
            ("Cromer_Ecstasy", "희열", "None", "None"),
            ("Cromer_Madness", "순수", "None", "None"),
            ("SlashTakeDamageDown", "참격 보호", "None", "None"),
            ("PenetrateTakeDamageDown", "관통 보호", "None", "None"),
            ("HitTakeDamageDown", "타격 보호", "None", "None"),
            ("SlashTakeDamageUp", "참격 취약", "None", "None"),
            ("PenetrateTakeDamageUp", "관통 취약", "None", "None"),
            ("HitTakeDamageUp", "타격 취약", "None", "None"),
            ("CrimsonTakeDamageDown", "분노 보호", "None", "None"),
            ("ScarletTakeDamageDown", "색욕 보호", "None", "None"),
            ("AmberTakeDamageDown", "나태 보호", "None", "None"),
            ("ShamrockTakeDamageDown", "탐식 보호", "None", "None"),
            ("AzureTakeDamageDown", "우울 보호", "None", "None"),
            ("IndigoTakeDamageDown", "오만 보호", "None", "None"),
            ("VioletTakeDamageDown", "질투 보호", "None", "None"),
            ("CrimsonTakeDamageUp", "분노 취약", "None", "None"),
            ("ScarletTakeDamageUp", "색욕 취약", "None", "None"),
            ("AmberTakeDamageUp", "나태 취약", "None", "None"),
            ("ShamrockTakeDamageUp", "탐식 취약", "None", "None"),
            ("AzureTakeDamageUp", "우울 취약", "None", "None"),
            ("IndigoTakeDamageUp", "오만 취약", "None", "None"),
            ("VioletTakeDamageUp", "질투 취약", "None", "None"),
            ("Desire", "욕망", "None", "None"),
            ("NailPersonality", "못", "None", "None"),
            ("AssemblePersonality", "광신", "None", "None"),
            ("MarkOfHeresy", "주시", "None", "None"),
            ("SlashResultUp", "참격 위력 증가", "None", "None"),
            ("PenetrateResultUp", "관통 위력 증가", "None", "None"),
            ("HitResultUp", "타격 위력 증가", "None", "None"),
            ("SlashResultDown", "참격 위력 감소", "None", "None"),
            ("PenetrateResultDown", "관통 위력 감소", "None", "None"),
            ("HitResultDown", "타격 위력 감소", "None", "None"),
            ("CrimsonResultUp", "분노 위력 증가", "None", "None"),
            ("ScarletResultUp", "색욕 위력 증가", "None", "None"),
            ("AmberResultUp", "나태 위력 증가", "None", "None"),
            ("ShamrockResultUp", "탐식 위력 증가", "None", "None"),
            ("AzureResultUp", "우울 위력 증가", "None", "None"),
            ("IndigoResultUp", "오만 위력 증가", "None", "None"),
            ("VioletResultUp", "질투 위력 증가", "None", "None"),
            ("CrimsonResultDown", "분노 위력 감소", "None", "None"),
            ("ScarletResultDown", "색욕 위력 감소", "None", "None"),
            ("AmberResultDown", "나태 위력 감소", "None", "None"),
            ("ShamrockResultDown", "탐식 위력 감소", "None", "None"),
            ("AzureResultDown", "우울 위력 감소", "None", "None"),
            ("IndigoResultDown", "오만 위력 감소", "None", "None"),
            ("VioletResultDown", "질투 위력 감소", "None", "None"),
            ("Cycle", "업보", "None", "None"),
            ("Duress", "구속", "None", "None"),
            ("WeaknessAnalysis", "약점 분석", "None", "None"),
            ("CyclingKarma", "순환하는 업보", "None", "None"),
            ("TakeHpHealReduce", "체력 회복 감소", "None", "None"),
            ("AttackLevelAdder", "공격 레벨+", "None", "None"),
            ("Thirst", "갈증", "None", "None"),
            ("GainSinStockAdder", "E.G.O 자원 획득량 +", "None", "None"),
            ("Weak_Chicken", "닭들의 종말 요리", "None", "None"),
            ("Strong_Chicken", "표현되지 않은 분노", "None", "None"),
            ("DimensionRift", "차원 균열", "None", "None"),
            ("VibrationExplosion", "진동 폭발", "None", "None"),
            ("ATL_Agility", "더 빠르게!", "None", "None"),
            ("ATL_Breath", "더 정확하게!", "None", "None"),
            ("ATL_Target", "저 놈 잡아!", "None", "None"),
            ("ElectricStorage", "축전", "None", "None"),
            ("SelfCharge", "자가 충전", "None", "None"),
            ("Exalted", "고양", "None", "None"),
            ("Tipsiness", "취기", "None", "None"),
            ("Predation", "포식", "None", "None"),
            ("Bull_FadedHeat", "잠시 사그라든 열기", "None", "None"),
            ("Bull_Fever", "흥분", "None", "None"),
            ("Bull_Sadness", "해소되지 않은 슬픔", "None", "None"),
            ("Bull_ReinforcedSadness", "팽창하는 슬픔", "None", "None"),
            ("Bull_BuzzingEmotion", "울렁이는 감정", "None", "None"),
            ("VibrationAssimilation", "진동 동화", "None", "None"),
            ("Resonate", "공진", "None", "None"),
            ("WrappedCurseTag", "얽혀온 저주 부적", "None", "None"),
            ("ReattachedCurseTag", "다시 붙여진 저주 부적", "None", "None"),
            ("Scared", "두려움", "None", "None"),
            ("Stressed", "스트레스", "None", "None"),
            ("Nervous", "초조함", "None", "None"),
            ("ShardOfUmbrella", "우산 파편", "None", "None"),
            ("Crazy_LowMorale", "광인", "None", "None"),
            ("Crazy_Panic", "광인", "None", "None"),
            ("Suicide_LowMorale", "자살", "None", "None"),
            ("Suicide_Panic", "자살", "None", "None"),
            ("Prowl_LowMorale", "배회", "None", "None"),
            ("Prowl_Panic", "배회", "None", "None"),
            ("Fragile_Mind_LowMorale", "위태로운 마음", "None", "None"),
            ("Fragile_Mind_Panic", "흐트러진 마음", "None", "None"),
            ("ErodingMind", "침식되는 마음", "None", "None"),
            ("Thorn", "가시", "None", "None"),
            ("CollapseAmpoule", "붕괴 앰플", "None", "None"),
            ("DongbaekGrow", "성장", "None", "None"),
            ("DongbaekFlorescence", "개화", "None", "None"),
            ("DongbaekFullBloom", "만개", "None", "None"),
            ("DongbaekFascination", "아찔한 정신", "None", "None"),
            ("DongbaekShowdown", "대결", "None", "None"),
            ("DongbaekScatterPetal", "흩어지는 꽃잎", "None", "None"),
            ("Blue_LowMorale", "슬픔", "None", "None"),
            ("Blue_Panic", "슬픔", "None", "None"),
            ("InterlockingTime", "맞물리는 시간", "None", "None"),
            ("TimeRental", "심상 속 시간 대여", "None", "None"),
            ("ATL_EndureLaceration", "출혈 저항", "None", "None"),
            ("EmittedCurrent", "방출 전류", "None", "None"),
            ("Choice_1040301", "알 수 없는 상처", "None", "None"),
            ("DistortedDongrangRadiantVanity", "빛나는 허영", "None", "None"),
            ("DistortedDongrangEmptyMark", "텅 빈 징표", "None", "None"),
            ("DistortedDongrangEarnestAchievement", "간절한 성과", "None", "None"),
            ("DistortedDongrangEmptyHonor", "공허한 영예", "None", "None"),
            ("DistortedDongrangMomentaryGlory", "찰나의 영광", "None", "None"),
            ("DistortedDongrangEmptiness", "공허함", "None", "None"),
            ("DistortedDongrangFruition", "맺힌 결실", "None", "None"),
            ("MpTakeDamageDown", "MpTakeDamageDown", "None", "None"),
            ("EgoAwakenDongrangRadiantDesire", "빛나는 욕망", "None", "None"),
            ("EgoAwakenDongrangSeed", "씨앗", "None", "None"),
            ("EgoAwakenDongrangTree", "양분 흡수", "None", "None"),
            ("EgoAwakenDongrangOverHeal", "과잉 회복", "None", "None"),
            ("EgoAwakenDongrangTreeDisplay", "양분 흡수", "None", "None"),
            ("DongbaekFullBloomDisplay", "만개", "None", "None"),
            ("SinkingSurge", "침잠쇄도", "None", "None"),
            ("Blue", "눈물", "None", "None"),
            ("EgoAwakenDongrangShardOfBrokenConnection", "부서진 인연의 조각", "None", "None"),
            ("KCorpSerum", "K사 앰플", "None", "None"),
            ("Talisman", "부적", "None", "None"),
            ("TakeHpHealIncrease", "체력 회복 증가", "None", "None"),
            ("Choice_90104001", "이해", "None", "None"),
            ("Choice_9010400101", "각오", "None", "None"),
            ("Choice_90104002", "회모", "None", "None"),
            ("SkillPowerUp", "기본 위력 증가", "None", "None"),
            ("MaxHpMultiplier", "최대 체력 증가", "None", "None"),
            ("S2Mirror2ndFloor", "거울의 거울 - 2층", "None", "None"),
            ("S2Mirror3rdFloor", "거울의 거울 - 3층", "None", "None"),
            ("S2Mirror4thFloor", "거울의 거울 - 4층", "None", "None"),
            ("S2Mirror5thFloor", "거울의 거울 - 5층", "None", "None"),
            ("S2Mirror2ndFloor_Hard", "거울의 거울 어려움 - 2층", "None", "None"),
            ("S2Mirror3rdFloor_Hard", "거울의 거울 어려움 - 3층", "None", "None"),
            ("S2Mirror4thFloor_Hard", "거울의 거울 어려움 - 4층", "None", "None"),
            ("S2Mirror5thFloor_Hard", "거울의 거울 어려움 - 5층", "None", "None"),
            ("S2Mirror1stFloor", "S2Mirror1stFloor", "None", "None"),
            ("S2Mirror1stFloor_Hard", "거울의 거울 어려움 - 1층", "None", "None"),
            ("MentalSystemResultIncrease_Typo", "정신력 회복 효율", "None", "None"),
            ("MentalSystemResultDecrease_Typo", "정신력 감소 효율", "None", "None"),
            ("DuelDeclaration_DonQuixote", "결투 선포 - 돈키호테", "None", "None"),
            ("Aggro", "도발치", "None", "None"),
            ("BurstVulnerable", "파열 취약", "None", "None"),
            ("EmergencyFeed", "긴급 영양 보급", "None", "None"),
            ("Bullet_Crab", "오래된 게딱지 탄", "None", "None"),
            ("SinkingVulnerable", "침잠 취약", "None", "None"),
            ("BurstProtection", "파열 보호", "None", "None"),
            ("ChargeForceField", "충전 역장", "None", "None"),
            ("RailLine2Buff", "거울 굴절", "None", "None"),
            ("TipsinessRail", "취기", "None", "None"),
            ("FairyCharm", "매혹", "None", "None"),
            ("ReattachedCurseTag_Re", "다시 붙여진 저주 부적", "None", "None"),
            ("PassedPortal_Red", "피가 흐르는 차원", "None", "None"),
            ("PassedPortal_Green", "독이 들끓는 차원", "None", "None"),
            ("PassedPortal_Yellow", "끊임없이 진동하는 차원", "None", "None"),
            ("PassedPortal_Blue", "전류를 뿜어내는 차원", "None", "None"),
            ("InterlockingTime_Re", "맞물리는 시간", "None", "None"),
            ("TimeRental_Re", "시간 대여", "None", "None"),
            ("WaveFoxUmbrella", "보호 우산", "None", "None"),
            ("AccumulatedPast", "축적된 과거", "None", "None"),
            ("PrepareSinRose", "면류관", "None", "None"),
            ("BloomingRose", "피어나는 죄", "None", "None"),
            ("EatingSin", "머금은 죄", "None", "None"),
            ("EnhanceRose", "강화 상태", "None", "None"),
            ("DecoyRegenerated", "재생", "None", "None"),
            ("FullBloomRose_Crimson", "FullBloomRose_Crimson", "None", "None"),
            ("FullBloomRose_Scarlet", "FullBloomRose_Scarlet", "None", "None"),
            ("FullBloomRose_Amber", "FullBloomRose_Amber", "None", "None"),
            ("FullBloomRose_Shamrock", "FullBloomRose_Shamrock", "None", "None"),
            ("FullBloomRose_Azure", "FullBloomRose_Azure", "None", "None"),
            ("FullBloomRose_Indigo", "FullBloomRose_Indigo", "None", "None"),
            ("FullBloomRose_Violet", "FullBloomRose_Violet", "None", "None"),
            ("KalpaVine", "억겁의 덩굴", "None", "None"),
            ("WrappedCurseTagRe", "얽혀온 저주 부적", "None", "None"),
            ("FullBloomRose", "피워낸 장미", "None", "None"),
            ("KnowledgeExplored", "탐구한 지식", "None", "None"),
            ("Discard", "버림", "None", "None"),
            ("AaCePaBa", "강박", "None", "None"),
            ("AaCePbBa", "창백한 소음", "None", "None"),
            ("AaCePbBb", "소음 공황", "None", "None"),
            ("AaCePbBi", "형님의 시험", "None", "None"),
            ("AaCePcBa", "표식", "None", "None"),
            ("AaCePcBb", "형광등 파편", "None", "None"),
            ("AaCePcBc", "맹목", "None", "None"),
            ("AaCePcBg", "유충", "None", "None"),
            ("AaCePcBh", "녹색 점액", "None", "None"),
            ("AaCePcBi", "거친 호흡", "None", "None"),
            ("AaCePcBj", "사냥감의 징표", "None", "None"),
            ("AaCePcBk", "결박", "None", "None"),
            ("AaCePcBl", "포획", "None", "None"),
            ("AaCePcBm", "AaCePcBm", "None", "None"),
            ("AaCePcBn", "거역할 수 없는 명령", "None", "None"),
            ("AaCePcBo", "핍의 자아", "None", "None"),
            ("AaCePcBp", "스타벅의 자아", "None", "None"),
            ("AaCePcBq", "퀴케그의 자아", "None", "None"),
            ("AaCePcBr", "원호 방어 보호막", "None", "None"),
            ("AaCePcBs", "원호 방어", "None", "None"),
            ("AaCePcBt", "원호 공격", "None", "None"),
            ("AaCePbBc", "불순", "None", "None"),
            ("AaCePbBd", "불순", "None", "None"),
            ("AaCePbBe", "앙갚음", "None", "None"),
            ("AaCePbBf", "앙갚음", "None", "None"),
            ("AaCePbBg", "원한", "None", "None"),
            ("AaCePbBh", "원한", "None", "None"),
            ("AaCePcBe", "하얀 공황", "None", "None"),
            ("AaCePcBf", "하얀 공황", "None", "None"),
            ("NoneMp_LowMorale", "NoneMp_LowMorale", "None", "None"),
            ("NoneMp_Panic", "NoneMp_Panic", "None", "None"),
            ("Unjust_Enrichment", "부당 수익", "None", "None"),
            ("Resentment", "Resentment", "None", "None"),
            ("RetaliationBook", "앙갚음 대상", "None", "None"),
            ("MDcFaBa", "화상 추가 부여", "None", "None"),
            ("MDcFaBb", "출혈 추가 부여", "None", "None"),
            ("MDcFaBc", "진동 추가 부여", "None", "None"),
            ("MDcFaBd", "파열 추가 부여", "None", "None"),
            ("MDcFaBe", "침잠 추가 부여", "None", "None"),
            ("MDcFaBf", "참격 강화", "None", "None"),
            ("MDcFaBg", "관통 강화", "None", "None"),
            ("MDcFaBh", "타격 강화", "None", "None"),
            ("MDcFbBa", "속성 내성 강화 (분노 제외)", "None", "None"),
            ("MDcFbBb", "속성 내성 강화 (색욕 제외)", "None", "None"),
            ("MDcFbBc", "속성 내성 강화 (나태 제외)", "None", "None"),
            ("MDcFbBd", "속성 내성 강화 (탐식 제외)", "None", "None"),
            ("MDcFbBe", "속성 내성 강화 (우울 제외)", "None", "None"),
            ("MDcFbBf", "속성 내성 강화 (오만 제외)", "None", "None"),
            ("MDcFbBg", "속성 내성 강화 (질투 제외)", "None", "None"),
            ("MDcFbBh", "체력 증진", "None", "None"),
            ("MDcFcBa", "슬롯 가중치 증가", "None", "None"),
            ("MDcFcBb", "피해량 흡수", "None", "None"),
            ("MDcFcBc", "속도 증가", "None", "None"),
            ("MDcFcBd", "정신 공격", "None", "None"),
            ("MDcFcBe", "예리함", "None", "None"),
            ("MDcFcBf", "재정비", "None", "None"),
            ("MDcFcBg", "일방 공격 강화", "None", "None"),
            ("MDcFcBh", "합 공격 강화", "None", "None"),
            ("MDHcFaBa", "방어 레벨 강화", "None", "None"),
            ("MDHcFaBb", "수비 위력 강화", "None", "None"),
            ("MDHcFaBc", "최대 체력 강화", "None", "None"),
            ("MDHcFaBd", "받는 피해량 경감", "None", "None"),
            ("MDHcFbBa", "공격 레벨 강화", "None", "None"),
            ("MDHcFbBb", "최종 위력 강화", "None", "None"),
            ("MDHcFbBc", "기본 위력 강화", "None", "None"),
            ("MDHcFbBd", "가하는 피해량 강화", "None", "None"),
            ("MDHcFcBa", "최종 위력 증폭 I", "None", "None"),
            ("MDHcFcBb", "최종 위력 증폭 II", "None", "None"),
            ("MDHcFcBc", "기본 위력 증폭 I", "None", "None"),
            ("MDHcFcBd", "기본 위력 증폭 II", "None", "None"),
            ("MDHcFcBe", "코인 위력 증폭 I", "None", "None"),
            ("MDHcFcBf", "과부하 I", "None", "None"),
            ("MDHcFdBa", "최종 위력 증폭 III", "None", "None"),
            ("MDHcFdBb", "최종 위력 증폭 IV", "None", "None"),
            ("MDHcFdBc", "코인 위력 증폭 II", "None", "None"),
            ("MDHcFdBd", "기본 위력 증폭 III", "None", "None"),
            ("MDHcFdBe", "과부하 II", "None", "None"),
            ("MDHcFdBf", "강인함", "None", "None"),
            ("MDHcFdBg", "완강함", "None", "None"),
            ("DuelDeclaration_Outis", "결투 선포 - 오티스", "None", "None"),
            ("DuelDeclaration_Sinclair", "결투 선포 - 싱클레어", "None", "None"),
            ("OneOnOneDuel", "1대1 대결", "None", "None"),
            ("AaCeSeBa", "호!호!호!", "None", "None"),
            ("AaCeSeBb", "선물로 만들어주마", "None", "None"),
            ("CtrlTeamCaptain", "지휘팀 팀장", "None", "None"),
            ("FreischutzShotCount", "마탄", "None", "None"),
            ("DarkFlame", "흑염", "None", "None"),
            ("MRcAiBa", "불씨", "None", "None"),
            ("MRcAiBb", "아른거리는 불씨", "None", "None"),
            ("MRcAiBc", "점화", "None", "None"),
            ("MRcAfBa", "목화", "None", "None"),
            ("MRcAfBb", "붉게 물든 목화", "None", "None"),
            ("MRcAmBa", "경멸", "None", "None"),
            ("MRcAmBb", "손아귀", "None", "None"),
            ("MRcAmBc", "시선", "None", "None"),
            ("MRcAmBd", "전부를 보는 시선", "None", "None"),
            ("MRcAmBbDisplay", "손아귀", "None", "None"),
            ("HeatedGasHarpoon", "가열된 가스 작살", "None", "None"),
            ("OverHeatedGasHarpoon", "과열된 가스 작살", "None", "None"),
            ("CoverAttack", "원호 공격", "None", "None"),
            ("ReCompulsion", "반전 강박", "None", "None"),
            ("Grudge", "맺혀가는 응어리", "None", "None"),
            ("BladeResultUpTier1", "본국검 - 세법 전수", "None", "None"),
            ("BladeResultUpTier2", "본국검 - 자법 전수", "None", "None"),
            ("RedApricotBlossom", "홍매화", "None", "None"),
            ("SwordPlayOfTheHomeland", "본국검술", "None", "None"),
            ("SwordPlayOfTheMemorial", "추모", "None", "None"),
            ("VibrationCollapse", "진동 - 붕괴", "None", "None"),
            ("AaCfPaBa", "상실", "None", "None"),
            ("CleanUp_LowMorale", "정리 강박", "None", "None"),
            ("CleanUp_Panic", "정리 강박", "None", "None"),
            ("Acclamation_LowMorale", "저택의 메아리", "None", "None"),
            ("Acclamation_Panic", "저택의 메아리", "None", "None"),
            ("Unstable_LowMorale", "분열", "None", "None"),
            ("Unstable_Panic", "분열", "None", "None"),
            ("EchoOfMansion", "저택의 메아리", "None", "None"),
            ("EchoOfMansion_Main", "저택의 메아리", "None", "None"),
            ("EchoOfMansion_Sub", "저택의 메아리", "None", "None"),
            ("Switch_Vibration", "진폭 변환", "None", "None"),
            ("Vengeance_LowMorale", "복수", "None", "None"),
            ("Vengeance_Panic", "복수", "None", "None"),
            ("AaCfPbBa", "폭발하는 우울", "None", "None"),
            ("AaCfPbBb", "폭발하는 질투", "None", "None"),
            ("AaCfPbBc", "폭발하는 분노", "None", "None"),
            ("AaCfPbBd", "깊은 상실", "None", "None"),
            ("AaCfPbBe", "사냥감 표시", "None", "None"),
            ("AaCfPbBf", "열등감", "None", "None"),
            ("AaCfPbBg", "흐트러진 그림", "None", "None"),
            ("AaCfPbBh", "흐트러진 그림", "None", "None"),
            ("AaCfPbBi", "증오", "None", "None"),
            ("AaCfPbBj", "증오", "None", "None"),
            ("AaCfPcBb", "버틀러의 표식", "None", "None"),
            ("AaCfPcBc", "포박술", "None", "None"),
            ("AaCfPcBa", "각오", "None", "None"),
            ("AaCfPcBe", "깊은 상실", "None", "None"),
            ("AaCfPcBf", "해방되지 못한 서글픔", "None", "None"),
            ("AaCfPcBg", "분노의 대상", "None", "None"),
            ("AaCfPcBh", "고통스러운 분노", "None", "None"),
            ("AaCfPcBi", "고열기전", "None", "None"),
            ("AaCfPcBj", "AaCfPcBj", "None", "None"),
            ("AaCfPcBk", "AaCfPcBk", "None", "None"),
            ("AaCfPcBl", "AaCfPcBl", "None", "None"),
            ("AaCfPcBm", "고통의 벼락", "None", "None"),
            ("AaCfPcBn", "찢어지는 마음", "None", "None"),
            ("AaCfPcBo", "각오", "None", "None"),
            ("AaCfPcBp", "다가갈 용기", "None", "None"),
            ("AaCfPcBq", "찢긴 마음", "None", "None"),
            ("AaCfPcBr", "고통 속에서...비참하게 깨어나기를!", "None", "None"),
            ("AaCfPcBs", "방해하지마", "None", "None"),
            ("Loss_LowMorale", "Loss_LowMorale", "None", "None"),
            ("Loss_Panic", "Loss_Panic", "None", "None"),
            ("ForwardToTheKing", "왕의 앞으로", "None", "None"),
            ("ForwardToTheBoundKing", "묶인 왕의 앞으로", "None", "None"),
            ("VibrationCrack", "진동 - 균열", "None", "None"),
            ("MarkOfButler", "버.표", "None", "None"),
            ("LibrarianOfHistoryNormal", "역사의 층 보조 사서", "None", "None"),
            ("PreparedMeat", "준비된 고기", "None", "None"),
            ("Hunger", "굶주림", "None", "None"),
            ("UnstableFeeling", "불안정한 격정", "None", "None"),
            ("MDEMaa", "방어 레벨 강화", "None", "None"),
            ("MDEMab", "수비 스킬 강화", "None", "None"),
            ("MDEMac", "공격 레벨 강화", "None", "None"),
            ("MDEMad", "최대 체력 강화", "None", "None"),
            ("MDEMae", "받는 피해량 감소 강화", "None", "None"),
            ("MDEMba", "MDEMba", "None", "None"),
            ("MDEMbb", "MDEMbb", "None", "None"),
            ("MDEMbc", "MDEMbc", "None", "None"),
            ("MDEMbd", "MDEMbd", "None", "None"),
            ("MDEMbe", "MDEMbe", "None", "None"),
            ("MDEMbf", "MDEMbf", "None", "None"),
            ("MDEMca", "육체 비대", "None", "None"),
            ("MDEMcb", "육체 강대", "None", "None"),
            ("MDEMcc", "최대 체력 강화", "None", "None"),
            ("MDEMcd", "최종 공격 증강", "None", "None"),
            ("MDEMce", "기본 공격 증강", "None", "None"),
            ("MDEMcf", "합 공격 증강", "None", "None"),
            ("MDEMcg", "최종 위력 강화", "None", "None"),
            ("MDEMda", "육체 신장", "None", "None"),
            ("MDEMdb", "육체 보강", "None", "None"),
            ("MDEMdc", "기본 전투 장비", "None", "None"),
            ("MDEMdd", "최종 전투 장비", "None", "None"),
            ("MDEMde", "방어 장비", "None", "None"),
            ("MDEMdf", "코인 공격 증강", "None", "None"),
            ("MDEMdg", "강대무비", "None", "None"),
            ("MDEMdh", "되와 말로 주기", "None", "None"),
            ("MDHMaa", "육체 증폭 Ⅰ", "None", "None"),
            ("MDHMab", "단단함", "None", "None"),
            ("MDHMac", "육체 확대 Ⅰ", "None", "None"),
            ("MDHMad", "최대 체력 증강 Ⅰ", "None", "None"),
            ("MDHMae", "받는 피해량 경감", "None", "None"),
            ("MDHMba", "육체 증폭 Ⅱ", "None", "None"),
            ("MDHMbb", "육체 확대 Ⅱ", "None", "None"),
            ("MDHMbc", "최종 위력 증강 Ⅰ", "None", "None"),
            ("MDHMbd", "기본 위력 증강 Ⅰ", "None", "None"),
            ("MDHMbe", "강인함 Ⅰ", "None", "None"),
            ("MDHMbf", "말로 주기Ⅰ", "None", "None"),
            ("MDHMca", "육체 증폭 Ⅲ", "None", "None"),
            ("MDHMcb", "육체 확대 Ⅲ", "None", "None"),
            ("MDHMcc", "최종 위력 증강 Ⅱ", "None", "None"),
            ("MDHMcd", "기본 위력 증강 Ⅱ", "None", "None"),
            ("MDHMce", "최종 위력 증강 Ⅲ", "None", "None"),
            ("MDHMcf", "최대 체력 증강 Ⅱ", "None", "None"),
            ("MDHMcg", "합 공격 증강 Ⅰ", "None", "None"),
            ("MDHMda", "육체 증폭  Ⅳ", "None", "None"),
            ("MDHMdb", "육체 확대  Ⅳ", "None", "None"),
            ("MDHMdc", "최종 위력 증강 Ⅳ", "None", "None"),
            ("MDHMdd", "기본 위력 증강 Ⅲ", "None", "None"),
            ("MDHMde", "강인함 Ⅱ", "None", "None"),
            ("MDHMdf", "코인 위력 증강", "None", "None"),
            ("MDHMdg", "합 공격 증강 Ⅱ", "None", "None"),
            ("MDHMdh", "완강함", "None", "None"),
            ("AccumulatedPastMirror", "축적된 과거", "None", "None"),
            ("LibrarianOfHistoryHard", "역사의 층 사서", "None", "None"),
            ("PriceOfCare", "채워지지 않는 허기", "None", "None"),
            ("CanvasA", "CanvasA", "None", "None"),
            ("CompletedCanvasA", "CompletedCanvasA", "None", "None"),
            ("VibrationEcho", "진동 - 반향", "None", "None"),
            ("ShieldManagerCryingToad", "울음 방울", "None", "None"),
            ("ParryingResultUp", "합 위력 증가", "None", "None"),
            ("ParryingResultDown", "합 위력 감소", "None", "None"),
            ("FusionVibration", "진폭 얽힘", "None", "None"),
            ("VibrationNesting", "진동 - 중첩", "None", "CollapsableSinBuff"),
            ("VibrationDistribution", "진동 - 분배", "None", "None"),
            ("VibrationChain", "진동 - 사슬", "None", "None"),
            ("TimeRentalTwo", "T사의 시간 대여", "None", "None"),
            ("TimeAccumulation", "시간 누진", "None", "None"),
            ("Yurodivy_LowMorale", "어긋난 단결", "None", "None"),
            ("Yurodivy_Panic", "어긋난 단결", "None", "None"),
            ("TcorpSpecialInvestigator", "T사 특별 수사관 배지", "None", "None"),
            ("TimeAcceleration", "시간 가속", "None", "None"),
            ("LimitedTime", "빼앗은 시간", "None", "None"),
            ("OwnTime", "분담 시간", "None", "None"),
            ("EquitableDistribution", "시간 공동 분담 지출", "None", "None"),
            ("UnfairDistribution", "내부 분열", "None", "None"),
            ("TimeKillerWatch", "시간 구제 대상", "None", "None"),
            ("VibrationContinue", "진동 - 영속", "None", "None"),
            ("TimeSuspend", "시간 유예", "None", "None"),
            ("VibrationChainPersonality", "진동 - 사슬", "None", "None"),
            ("TimeRentalTwoPersonality", "시간 대여", "None", "None"),
            ("GazePersonality", "경멸의 시선", "None", "None"),
            ("ContemptPersonality", "시선의 경멸", "None", "None"),
            ("KnowledgeTraining", "지식 단련", "None", "None"),
            ("UninvitedGuest", "불청객", "None", "None"),
            ("ConnectedPlug", "연결된 플러그", "None", "None"),
            ("AntiSheepGround", "대양전 접지 플러그", "None", "None"),
            ("ThundercloudFormation", "전운집", "None", "None"),
            ("BandageOfTheBoundKing", "묶인 왕의 붕대", "None", "None"),
            ("ComeForwardToTheKing", "알현의 시간", "None", "None"),
            ("Refraction4A", "1번 편성 효과", "None", "None"),
            ("Refraction4B", "2번 편성 효과", "None", "None"),
            ("Refraction4C", "3번 편성 효과", "None", "None"),
            ("Refraction4D", "4번 편성 효과", "None", "None"),
            ("Refraction4E", "5번 편성 효과", "None", "None"),
            ("Refraction4F", "6번 편성 효과", "None", "None"),
            ("Refraction4G", "7번 편성 효과", "None", "None"),
            ("Refraction4H", "8번 편성 효과", "None", "None"),
            ("Refraction4I", "9번 편성 효과", "None", "None"),
            ("Refraction4J", "10번 편성 효과", "None", "None"),
            ("Refraction4K", "11번 편성 효과", "None", "None"),
            ("Refraction4L", "12번 편성 효과", "None", "None"),
            ("VioletPeccatulumTwo", "질투 죄종 2형", "None", "None"),
            ("BigWelcome", "성대한 환대", "None", "None"),
            ("RefractedWill", "굴절된 의지", "None", "None"),
            ("BreathSupport", "결전의 호흡", "None", "None"),
            ("FirmWill", "굳은 의지", "None", "None"),
            ("ChargeLoad", "부하", "None", "None"),
            ("BoseProjektil", "찢어진 추억", "None", "None"),
            ("KeptBlood", "머금은 피", "None", "None"),
            ("BloodyCrave", "핏빛 갈구", "None", "None"),
            ("BloodPocket_LowMorale", "흡혈 갈망", "None", "None"),
            ("BloodPocket_Panic", "흡혈 갈망", "None", "None"),
            ("PhotoElectricity", "광전", "None", "None"),
            ("HardenedBlood", "경화 혈액", "None", "None"),
            ("Coffin", "관", "None", "None"),
            ("WildHunt", "와일드헌트", "None", "None"),
            ("NightPathfinding", "듀라한", "None", "None"),
            ("WanderingFootsteps", "다가오는 파탄", "None", "None"),
            ("WanderingFootsteps_Main", "다가오는 파탄", "None", "None"),
            ("WanderingFootsteps_Sub", "다가오는 파탄", "None", "None"),
            ("WanderingFootsteps_LowMorale", "파탄", "None", "None"),
            ("WanderingFootsteps_Panic", "파탄", "None", "None"),
            ("AaCfPaBa_Alt1", "끝없는 상실", "None", "None"),
            ("AaCfPaBa_Alt2", "히스클리프들의 목을 벤다", "None", "None"),
            ("AaCfPcBa_Alt1", "쏟아내는 분노", "None", "None"),
            ("AaCfPcBa_Alt2", "히스클리프들의 목을 벤다", "None", "None"),
            ("AaCfPcBa_Alt3", "애정과 증오", "None", "None"),
            ("AaCfPcBa_Alt4", "색바랜 약속", "None", "None"),
            ("AaCfPcBa_Alt5", "색바랜 약속", "None", "None"),
            ("TrainTeamCaptain", "교육팀 팀장", "None", "None"),
            ("VioletUnderstand", "이해", "None", "None"),
            ("MentalCrack", "정신 균열", "None", "None"),
            ("ImpendingCollapse", "붕괴 임박", "None", "None"),
            ("SinkingWhite", "나비", "None", "None"),
            ("BulletLament", "산나비·죽은나비", "None", "None"),
            ("ReloadLament", "재장전", "None", "None"),
            ("RedEyeFirst", "적안", "None", "None"),
            ("RedEyeSecond", "적안 - 경계", "None", "None"),
            ("RedEyeThird", "적안 - 포식", "None", "None"),
            ("PenanceFirst", "참회", "None", "None"),
            ("PenanceSecond", "참회 - 경계", "None", "None"),
            ("PenanceThird", "참회 - 고해", "None", "None"),
            ("Emptiness", "하얗게 태워버렸군", "None", "None"),
            ("UninvitedGuestPersonality", "초청받지 않은 자", "None", "None"),
            ("DevyatDimensionalSack", "딜리버리 캐리어 - 로쟈", "None", "None"),
            ("Retreat", "전략적 휴식 복지 모드", "None", "None"),
            ("DefensiveStance", "방어 태세", "None", "None"),
            ("CanDuelGuard", "합 가능 가드", "None", "None"),
            ("SuperCoin", "파괴 불가 코인", "None", "None"),
            ("DuelDeclaration_Camille", "결투 선포 - 까미유", "None", "None"),
            ("RecklessDuel", "무모한 결투", "None", "None"),
            ("ConcentratedAttack", "집중 공격", "None", "None"),
            ("BloodScissor", "핏빛 가위날", "None", "None"),
            ("LineCutting", "재봉 대상", "None", "None"),
            ("BloodScissorScars", "깊게 베인 상처", "None", "None"),
            ("BloodDinner", "혈찬", "None", "None"),
            ("FamineBlood_LowMorale", "굶주림", "None", "None"),
            ("FamineBlood_Panic", "굶주림", "None", "None"),
            ("Duello_LowMorale", "1:1 전력", "None", "None"),
            ("Duello_Panic", "1:1 전력", "None", "None"),
            ("ScissorCutting", "ScissorCutting", "None", "None"),
            ("TrulyWeak", "약자", "None", "None"),
            ("RealPaperBear", "진짜 곰", "None", "None"),
            ("BloodScissorTwo", "핏빛 가위날 II", "None", "None"),
            ("BloodScissorThree", "핏빛 가위날 III", "None", "None"),
            ("StarvingBarberOne", "노쇠", "None", "None"),
            ("StarvingBarberTwo", "노쇠", "None", "None"),
            ("ConcentratedAttackMeursault", "집중 공격 - 뫼르소", "None", "None"),
            ("BloodDinner_Accumulation", "누적 소모 혈찬", "None", "None"),
            ("BloodShooting", "블러드슈팅!!", "None", "None"),
            ("RighteousFeeling", "정의로운 기분이라네!!", "None", "None"),
            ("BloomingThorns", "피어나는 가시", "None", "None"),
            ("BloomingThorns_2nd", "피를 엮은 가시", "None", "None"),
            ("BloomingThorns_3rd", "피로 빚은 가시", "None", "None"),
            ("FestivalFever", "축제의 열기", "None", "None"),
            ("IncompleteParade", "불완전한 퍼레이드", "None", "None"),
            ("BloodyHand", "피로 물든 손", "None", "None"),
            ("FamineBloodDolciServant_LowMorale", "FamineBloodDolciServant_LowMorale", "None", "None"),
            ("FamineBloodDolciServant_Panic", "FamineBloodDolciServant_Panic", "None", "None"),
            ("StarvingDolcineaServant", "노쇠", "None", "None"),
            ("FamineBloodDolci_LowMorale", "허무의 굴레", "None", "None"),
            ("FamineBloodDolci_Panic", "허무의 굴레", "None", "None"),
            ("StarvingDolcineaOne", "노쇠", "None", "None"),
            ("StarvingPriestOne", "노쇠", "None", "None"),
            ("HonorableDuel_Don", "명예로운 결투", "None", "None"),
            ("HonorableDuel_Knight", "명예로운 결투", "None", "None"),
            ("Snare", "올가미", "None", "None"),
            ("ThornyFall_LowMorale", "가시", "None", "None"),
            ("ThornyFall_Panic", "가시", "None", "None"),
            ("LineCuttingPersonality", "재봉 대상", "None", "None"),
            ("BloodScissorPersonalityFirst", "핏빛 가위날", "None", "None"),
            ("BloodScissorPersonalitySecond", "핏빛 가위날 II", "None", "None"),
            ("BloodScissorPersonalityThird", "핏빛 가위날 III", "None", "None"),
            ("UnstoppableFunny", "멈출 수 없는 환희", "None", "None"),
            ("ThornNoose", "가시 올가미", "None", "None"),
            ("TinyCarmilla", "작은 카르밀라", "None", "None"),
            ("OneDropNutrition", "한 방울의 양분", "None", "None"),
            ("BloodringUp_LowMorale", "피올림", "None", "None"),
            ("BloodringUp_Panic", "피올림", "None", "None"),
            ("Guilt_LowMorale", "죄책감", "None", "None"),
            ("Guilt_Panic", "죄책감", "None", "None"),
            ("WornHeart", "닳아버린 마음", "None", "None"),
            ("BloodyHand_2nd", " 피로 물든 손 II", "None", "None"),
            ("BloodyHand_3rd", " 피로 물든 손 III", "None", "None"),
            ("StarvingPriestTwo", "노쇠", "None", "None"),
            ("MentalIncreaseDown", "정신력 회복 효율 감소", "None", "None"),
            ("AffectionTeddy", "애착", "None", "None"),
            ("FaintMemory", "아련한 기억", "None", "None"),
            ("CursePackage", "저주 꾸러미", "None", "None"),
            ("BloodArmor", "경혈", "None", "None"),
            ("BloodArmor_2nd", "경혈 II", "None", "None"),
            ("BloodArmor_3rd", "경혈 III", "None", "None"),
            ("Dreamy", "꿈결속 망설임", "None", "None"),
            ("SwirlingBlood", "일렁임【혈귀】", "None", "None"),
            ("SanchoMind_LowMorale", "깨어나는 살육의 고양감", "None", "None"),
            ("SanchoMind_Panic", "깨어나는 살육의 고양감", "None", "None"),
            ("RoseWedge", "장미 쐐기", "None", "None"),
            ("ThirstyRose", "목마른 장미", "None", "None"),
            ("FerrisWheel", "돌고도는 관람차", "None", "None"),
            ("StarvingDonqui", "굶주림과 노쇠", "None", "None"),
            ("RegainedStrength", "다시 도는 피의 충동", "None", "None"),
            ("RegainedStrength_2nd", "다시 도는 피의 충동 II", "None", "None"),
            ("RegainedStrength_3rd", "다시 도는 피의 충동 III", "None", "None"),
            ("Precarious", "무책임한 꿈", "None", "None"),
            ("Penetration", "굳어가는 피", "None", "None"),
            ("WeightOfResponsibility_LowMorale", "내 꿈을 버리고 이제는...", "None", "None"),
            ("WeightOfResponsibility_Panic", "어버이로써 가족을 위해....", "None", "None"),
            ("LacerationSurge", "LacerationSurge", "None", "None"),
            ("BloodDinner_Common_Accumulation", "공용 누적 소모 혈찬", "None", "None"),
            ("BloodyHandGregFirst", "피로 물든 손", "None", "None"),
            ("BloodyHandGregSecond", "피로 물든 손 II", "None", "None"),
            ("BloodyHandGregThird", "피로 물든 손 III", "None", "None"),
            ("WornHeartGreg", "닳아버린 마음", "None", "None"),
            ("BloomingThornsRodionFirst", "피어나는 가시", "None", "None"),
            ("BloomingThornsRodionSecond", "피어나는 가시 II", "None", "None"),
            ("BloomingThornsRodionThird", "피어나는 가시 III", "None", "None"),
            ("FestivalFeverRodion", "축제의 열기", "None", "None"),
            ("DecreamentalDefense", "심각한 부상", "None", "None"),
            ("UnfinishedDream", "물보다 진한 피에서 벗어나,", "None", "None"),
            ("UnfinishedDreamTwo", "UnfinishedDreamTwo", "None", "None"),
            ("FragmentOfHope", "버스에서 동료들과 함께 여러 모험을 겪었으며,", "None", "None"),
            ("FragmentOfHopeTwo", "꿈을 끝내지 않을 산초이자 우리의 돈키호테에 대하여", "None", "None"),
            ("ConfinementOfGoldenBranch", "황금가지의 강제 조율", "None", "None"),
            ("DevyatDimensionalSackSinclair", "딜리버리 캐리어 - 싱클레어", "None", "None"),
            ("VibrationSpring", "진동 - 태엽감기", "None", "None"),
            ("MD5Base", "추가되는 고난", "None", "None"),
            ("MD511", "공격 레벨 강화 I", "None", "None"),
            ("MD512", "방어 레벨 강화", "None", "None"),
            ("MD513", "강인함 I", "None", "None"),
            ("MD514", "성장 I", "None", "None"),
            ("MD515", "수비 스킬 강화", "None", "None"),
            ("MD516", "받는 피해량 감소 강화", "None", "None"),
            ("MD521", "공격 레벨 강화 II", "None", "None"),
            ("MD522", "육체 강화 I", "None", "None"),
            ("MD523", "예리 I", "None", "None"),
            ("MD524", "강인함 II", "None", "None"),
            ("MD525", "강인함 III", "None", "None"),
            ("MD526", "성장 II", "None", "None"),
            ("MD527", "완강함 I", "None", "None"),
            ("MD531", "예리 II", "None", "None"),
            ("MD532", "예리 III", "None", "None"),
            ("MD533", "강인함 IV", "None", "None"),
            ("MD534", "강인함 V", "None", "None"),
            ("MD535", "성장 III", "None", "None"),
            ("MD536", "합 위력 증폭", "None", "None"),
            ("MD537", "최종 위력 증폭", "None", "None"),
            ("MD538", "기본 위력 증폭", "None", "None"),
            ("MD541", "예리 IV", "None", "None"),
            ("MD542", "육체 강화 II", "None", "None"),
            ("MD543", "육체 강화 III", "None", "None"),
            ("MD544", "성장 IV", "None", "None"),
            ("MD545", "합 위력 증강", "None", "None"),
            ("MD546", "최종 위력 증강", "None", "None"),
            ("MD547", "기본 위력 증강", "None", "None"),
            ("MD548", "파괴력", "None", "None"),
            ("MD549", "완강함 II", "None", "None"),
            ("MD551", "예리 V", "None", "None"),
            ("MD552", "육체 강화 IV", "None", "None"),
            ("MD553", "육체 강화 V", "None", "None"),
            ("MD554", "성장 V", "None", "None"),
            ("MD555", "합 위력 증강 II", "None", "None"),
            ("MD556", "최종 위력 증강 II", "None", "None"),
            ("MD557", "기본 위력 증강 II", "None", "None"),
            ("MD558", "파괴력 II", "None", "None"),
            ("MD561", "예리 VI", "None", "None"),
            ("MD562", "강인함 VI", "None", "None"),
            ("MD563", "육체 강화 VI", "None", "None"),
            ("MD564", "성장 VI", "None", "None"),
            ("MD565", "합 위력 증강 III", "None", "None"),
            ("MD566", "최종 위력 증강 III", "None", "None"),
            ("MD567", "기본 위력 증강 III", "None", "None"),
            ("MD568", "파괴력 III", "None", "None"),
            ("MD571", "예리 VII", "None", "None"),
            ("MD572", "강인함 VII", "None", "None"),
            ("MD573", "육체 강화 VII", "None", "None"),
            ("MD574", "성장 VII", "None", "None"),
            ("MD575", "합 위력 증강 IV", "None", "None"),
            ("MD576", "최종 위력 증강 IV", "None", "None"),
            ("MD577", "기본 위력 증강 IV", "None", "None"),
            ("MD578", "파괴력 IV", "None", "None"),
            ("MD581", "예리 VIII", "None", "None"),
            ("MD582", "강인함 VIII", "None", "None"),
            ("MD583", "육체 강화 VIII", "None", "None"),
            ("MD584", "성장 VIII", "None", "None"),
            ("MD585", "합 위력 증강 V", "None", "None"),
            ("MD586", "최종 위력 증강 V", "None", "None"),
            ("MD587", "기본 위력 증강 V", "None", "None"),
            ("MD588", "파괴력 V", "None", "None"),
            ("MD591", "예리 IX", "None", "None"),
            ("MD592", "강인함 IX", "None", "None"),
            ("MD593", "육체 강화 IX", "None", "None"),
            ("MD594", "성장 IX", "None", "None"),
            ("MD595", "합 위력 증강 VI", "None", "None"),
            ("MD596", "최종 위력 증강 VI", "None", "None"),
            ("MD597", "기본 위력 증강 VI", "None", "None"),
            ("MD598", "파괴력 VI", "None", "None"),
            ("Zazen", "참선", "None", "None"),
            ("SwirlingBloodPersonality", "일렁임【혈귀】", "None", "None"),
            ("BloodArmorPersonalityFirst", "경혈", "None", "None"),
            ("BloodArmorPersonalitySecond", "경혈 II", "None", "None"),
            ("BloodArmorPersonalityThird", "경혈 III", "None", "None"),
            ("RighteousFeelingSancho", "피눈물을 머금고, 내가 책임지겠다.", "None", "None"),
            ("UnfinishedDreamSancho", "물보다 진한 피에서 벗어나지 못하여,", "None", "None"),
            ("FragmentOfHopeSancho", "고통 받은 가족들을 위해 피의 만찬을 열고,", "None", "None"),
            ("FragmentOfHopeTwoSancho", "자식을 등진 어버이를 처단하여 모든 죄악감을 짊어진 어느 혈귀에 대하여", "None", "None"),
            ("SadLamanchaland", "책임감", "None", "None"),
            ("FreishutzOutisEgoBullet_1st", "첫번째 마탄", "None", "None"),
            ("FreishutzOutisEgoBullet_2nd", "두번째 마탄", "None", "None"),
            ("FreishutzOutisEgoBullet_3rd", "세번째 마탄", "None", "None"),
            ("FreishutzOutisEgoBullet_4th", "네번째 마탄", "None", "None"),
            ("FreishutzOutisEgoBullet_5th", "다섯번째 마탄", "None", "None"),
            ("FreishutzOutisEgoBullet_6th", "여섯번째 마탄", "None", "None"),
            ("FreishutzOutisEgoBullet_7th", "일곱번째 마탄", "None", "None"),
            ("FreishutzOutisEgoBulletCnt", "사수의 탄환", "None", "None"),
            ("Smoke", "연기", "None", "None"),
            ("Church_LowMorale", "교단", "None", "None"),
            ("Church_Panic", "교단", "None", "None"),
            ("MeatGearForce", "고기 톱니:강제", "None", "None"),
            ("AllSetForShooting", "사격 준비 만전", "None", "None"),
            ("CoveringFire", "타겟 포착", "None", "None"),
            ("FullReload", "재장전", "None", "None"),
            ("RequestedTarget", "의뢰 대상", "None", "None"),
            ("Bullet_LogicAtelier", "탄환 - 로직 아틀리에", "None", "None"),
            ("LogicAtelierAM", "집중【저격】", "None", "None"),
            ("MeleeCover", "근접 지원", "None", "None"),
            ("Retreat_FullStop", "후방 지원 배치", "None", "None"),
            ("ObservedPerson", "피관찰자", "None", "None"),
            ("Hohenheim_LowMorale", "고뇌", "None", "None"),
            ("Hohenheim_Panic", "고뇌", "None", "None"),
            ("Bloodthirst", "피갈망", "None", "None"),
            ("GlowingLantern", "미끼 요정", "None", "None"),
            ("A1c971a", "끓어오르는 분노", "None", "None"),
            ("A1c971b", "퍼져나가는 우울", "None", "None"),
            ("A1c971c", "계속되는 탐식", "None", "None"),
            ("A1c971d", "뱉어내는 호흡", "None", "None"),
            ("A1c971e", "상처속의 상처", "None", "None"),
            ("HoldingBreath", "깊은 들숨", "None", "None"),
            ("NebulizerInhale", "네뷸라이저 β", "None", "None"),
            ("A1c971f", "게으른 나태", "None", "None"),
            ("BlackCloud", "검은 구름", "None", "None"),
            ("BlackCloudBlade", "흑운도", "None", "None"),
            ("EngageToBattle", "임전", "None", "None"),
            ("CloudWall", "구름 장벽", "None", "None"),
            ("FellBulletMark", "징표", "None", "None"),
            ("FellBulletGroggy", "타겟 설정", "None", "None"),
            ("NebulizerExhale", "네뷸라이저 α", "None", "None"),
            ("ReleaseBreath", "가쁜 날숨", "None", "None"),
            ("MRR5BaseP", "적용 중인 버프", "None", "None"),
            ("MRR5BaseN", "적용 중인 고난", "None", "None"),
            ("MRR501", "부여량 굴절", "None", "None"),
            ("MRR502", "획득량 굴절", "None", "None"),
            ("MRR503", "굴절된 정신", "None", "None"),
            ("MRR504", "굴절된 호흡", "None", "None"),
            ("MRR505", "굴절된 충전", "None", "None"),
            ("MRR506", "굴절된 폭발", "None", "None"),
            ("MRR507", "굴절된 파열", "None", "None"),
            ("MRR508", "굴절된 침잠", "None", "None"),
            ("MRR509", "약점 파괴", "None", "None"),
            ("MRR509P", "MRR509P", "None", "None"),
            ("MRR509E", "MRR509E", "None", "None"),
            ("MRR510", "네뷸라이저", "None", "None"),
            ("MRR510P", "MRR510P", "None", "None"),
            ("MRR510E", "MRR510E", "None", "None"),
            ("MRR511", "고독한 사수", "None", "None"),
            ("MRR511P", "MRR511P", "None", "None"),
            ("MRR511E", "MRR511E", "None", "None"),
            ("MRR512", "이어지는 투지", "None", "None"),
            ("MRR513", "자원 수확", "None", "None"),
            ("MRR514", "버프 이름", "None", "None"),
            ("MRR515", "완강한 방어", "None", "None"),
            ("MRR516", "사생결단", "None", "None"),
            ("MRR517", "격노한 반격", "None", "None"),
            ("MRR518", "추억의 펜던트", "None", "None"),
            ("MRR518P", "MRR518P", "None", "None"),
            ("MRR518E", "MRR518E", "None", "None"),
            ("MRR519", "버프 이름", "None", "None"),
            ("MRR519P", "MRR519P", "None", "None"),
            ("MRR519E", "MRR519E", "None", "None"),
            ("MRR520", "흉탄 쇄도", "None", "None"),
            ("MRR520P", "MRR520P", "None", "None"),
            ("MRR520E", "MRR520E", "None", "None"),
            ("MRR521", "체력 왜곡", "None", "None"),
            ("MRR522", "위태로운 승부", "None", "None"),
            ("MRR523", "핏빛 연회", "None", "None"),
            ("MRR524", "피의 축제", "None", "None"),
            ("MRR524P", "MRR524P", "None", "None"),
            ("MRR524E", "MRR524E", "None", "None"),
            ("MRR525", "MRR525", "None", "None"),
            ("MRR526", "굴절된 참격", "None", "None"),
            ("MRR526P", "MRR526P", "None", "None"),
            ("MRR526E", "MRR526E", "None", "None"),
            ("MRR527", "굴절된 관통", "None", "None"),
            ("MRR527P", "MRR527P", "None", "None"),
            ("MRR527E", "MRR527E", "None", "None"),
            ("MRR528", "굴절된 타격", "None", "None"),
            ("MRR528P", "MRR528P", "None", "None"),
            ("MRR528E", "MRR528E", "None", "None"),
            ("MRR529", "성장 굴절 I", "None", "None"),
            ("MRR530", "성장 굴절 II", "None", "None"),
            ("MRR531", "버프 이름", "None", "None"),
            ("MRR532", "자원 휘발", "None", "None"),
            ("MRR533", "만발한 피", "None", "None"),
            ("MRR534", "완강한 방어", "None", "None"),
            ("MRR535", "사생결단", "None", "None"),
            ("MRR536", "굴절된 격노", "None", "None"),
            ("MRR537", "체력 굴절 II", "None", "None"),
            ("MRR538", "버프 이름", "None", "None"),
            ("MRR539", "핏빛 연회", "None", "None"),
            ("MRR540", "버프 이름", "None", "None"),
            ("MRR541", "버프 이름", "None", "None"),
            ("MRR542", "체력 굴절 I", "None", "None"),
            ("MRR543", "이어지는 의지", "None", "None"),
            ("MRR544", "위력 룰렛", "None", "None"),
            ("MRR545", "유리 대포", "None", "None"),
            ("MRR546", "하이리스크", "None", "None"),
            ("MRR547", "위력 룰렛", "None", "None"),
            ("MRR548", "위태로운 승부", "None", "None"),
            ("MRR549", "사생결단", "None", "None"),
            ("MRR550", "하이리스크", "None", "None"),
            ("MRR551", "굴절된 출혈", "None", "None"),
            ("MRR552", "굴절된 투지", "None", "None"),
            ("MRR553", "한 번의 숨", "None", "None"),
            ("MRR553P", "MRR553P", "None", "None"),
            ("MRR553E", "MRR553E", "None", "None"),
            ("MRR554", "사생결단", "None", "None"),
            ("MRR555", "합 위력 굴절", "None", "None"),
            ("MRR556", "유리 대포", "None", "None"),
            ("MRR557", "MRR557", "None", "None"),
            ("MRR558", "MRR558", "None", "None"),
            ("MRR559", "MRR559", "None", "None"),
            ("MRR560", "MRR560", "None", "None"),
            ("MRR561", "MRR561", "None", "None"),
            ("MRR562", "MRR562", "None", "None"),
            ("MRR563", "MRR563", "None", "None"),
            ("MRR564", "MRR564", "None", "None"),
            ("MRR565", "MRR565", "None", "None"),
            ("MRR566", "MRR566", "None", "None"),
            ("MRR567", "MRR567", "None", "None"),
            ("MRR568", "MRR568", "None", "None"),
            ("MRR569", "MRR569", "None", "None"),
            ("DianxueDonQuixote", "점혈 - 돈키호테", "None", "None"),
            ("FirePunchFuel", "12구산 연료", "None", "None"),
            ("FirePunchFuelOverheated", "과열 연료", "None", "None"),
            ("CandyForCharon", "사탕?", "None", "None"),
            ("TickTockTickTock", "째깍째깍?!", "None", "None"),
            ("CandyForCharon_LowMorale", "사탕", "None", "None"),
            ("CandyForCharon_Panic", "사탕", "None", "None"),
            ("MiddleFingerBook", "중지의 복수 대상", "None", "None"),
            ("DarkBeast_LowMorale", "암수", "None", "None"),
            ("DarkBeast_Panic", "암수", "None", "None"),
            ("BurstAgility", "주살【신속】", "None", "None"),
            ("RepressedMurderousIntend", "억눌린 살기", "None", "None"),
            ("WeighedMurderousIntend", "짓눌린 살기", "None", "None"),
            ("LegStrength", "각력【묘】", "None", "None"),
            ("Persistent", "끈질김", "None", "None"),
            ("BackstreetsNight_LowMorale", "뒷골목의 밤", "None", "None"),
            ("BackstreetsNight_Panic", "뒷골목의 밤", "None", "None"),
            ("SweeperA", "제 1파", "None", "None"),
            ("SweeperB", "제 2파", "None", "None"),
            ("SweeperC", "제 3파", "None", "None"),
            ("VengeanceBookSinclair", "앙갚음 장부 [싱클레어]", "None", "None"),
            ("VendettaMark", "복수 대상", "None", "None"),
            ("RetaliationBookFamily", "형제자매의 앙갚음 대상", "None", "None"),
            ("BirdCage", "달궈진 새장", "None", "None"),
            ("WideAreaRampage", "광역 난사", "None", "None"),
            ("ActivatedEgoPassive", "E.G.O 패시브", "None", "None"),
            ("TheDrifter_LowMorale", "박도", "None", "None"),
            ("TheDrifter_Panic", "박도", "None", "None"),
            ("SipOfAlcohol", "한 모금", "None", "None"),
            ("BurstPoison", "주살【독】", "None", "None"),
            ("SnakeStance", "사완", "None", "None"),
            ("EntangledCurseTalisman", "얽혀버린 저주 부적", "None", "None"),
            ("CentipedePoison", "지네 독", "None", "None"),
            ("WitheredWood_LowMorale", "고목화", "None", "None"),
            ("WitheredWood_Panic", "고목화", "None", "None"),
            ("EvilHeart_LowMorale", "사심", "None", "None"),
            ("EvilHeart_Panic", "사심", "None", "None"),
            ("DeepEvilHeart_LowMorale", "짙은 사심", "None", "None"),
            ("DeepEvilHeart_Panic", "짙은 사심", "None", "None"),
            ("OminousTalisman", "불길한 부적", "None", "None"),
            ("ProtectStance", "보호 태세", "None", "None"),
            ("TakeBreath_LowMorale", "숨 돌리기", "None", "None"),
            ("TakeBreath_Panic", "숨 돌리기", "None", "None"),
            ("RepressedMurderousIntendTwo", "눌린 살기", "None", "None"),
            ("BurstWeakness", "주살【약화】", "None", "None"),
            ("ZiluDebuff", "살기 억제", "None", "None"),
            ("UncontrolledChargeAxe_LowMorale", "충전 회로 역류", "None", "None"),
            ("UncontrolledChargeAxe_Panic", "충전 회로 역류", "None", "None"),
            ("Qiu_LowMorale", "가치우", "None", "None"),
            ("Qiu_Panic", "가치우", "None", "None"),
            ("ExaminationOfQiu", "일말의 기대", "None", "None"),
            ("BurstStop", "주살【물동】", "None", "None"),
            ("BurstSuppress", "주살【철주】", "None", "None"),
            ("BurstWave", "주살【즉감】", "None", "None"),
            ("Teaching", "문답", "None", "None"),
            ("JarMaster_LowMorale", "돌격 지시", "None", "None"),
            ("JarMaster_Panic", "돌격 지시", "None", "None"),
            ("BurstZilu", "주살【파】", "None", "None"),
            ("SupportProtect", "원호 방어", "None", "None"),
            ("SupportProtectTypo", "원호 방어", "None", "None"),
            ("EnhanceZilu", "천구성도", "None", "None"),
            ("BurstVulnerableZilu", "붕괴 표식", "None", "None"),
            ("DarkBeastSnake_LowMorale", "암완", "None", "None"),
            ("DarkBeastSnake_Panic", "암완", "None", "None"),
            ("NEgoFleshSpatula_LowMorale", "살점 주걱", "None", "None"),
            ("NEgoFleshSpatula_Panic", "살점 주걱", "None", "None"),
            ("EgoErodeContempt_LowMorale", "여린 금으로 주조한 마음", "None", "None"),
            ("EgoErodeContempt_Panic", "여린 금으로 주조한 마음", "None", "None"),
            ("GazeReplica", "시선", "None", "None"),
            ("ContemptReplica", "경멸", "None", "None"),
            ("GraspReplica", "손아귀", "None", "None"),
            ("EgoErodeMemory_LowMorale", "새 시대로 향하는 마음", "None", "None"),
            ("EgoErodeMemory_Panic", "새 시대로 향하는 마음", "None", "None"),
            ("EgoErodeReplica", "E.G.O 침식 - N사", "None", "None"),
            ("BoseProjektilReplica", "찢어진 추억", "None", "None"),
            ("LeRegole_LowMorale", "규율", "None", "None"),
            ("LeRegole_Panic", "규율", "None", "None"),
            ("BudgetBulletPropellant", "추진탄", "None", "None"),
            ("YisangParryGubo", "책임감", "None", "None"),
            ("HongluParryGahwan", "마주하려는 의지", "None", "None"),
            ("GahwanEgoContempt", "불안정 E.G.O 감응 - 경멸", "None", "None"),
            ("GuboEgoShooter", "불안정 E.G.O 감응 - 흉탄", "None", "None"),
            ("FellBulletMarkReplica", "징표", "None", "None"),
            ("DragonLance", "매화첨[埋花櫼]", "None", "None"),
            ("CondensedBlood", "오혈", "None", "None"),
            ("YesdragonSelf", "매화침[埋花針]", "None", "None"),
            ("YesdragonNodie", "불허[不許]", "None", "None"),
            ("YesdragonBurst", "칼날 가시", "None", "None"),
            ("QiuAndHonglu", "나의 진짜 마음…", "None", "None"),
            ("BeastEyes_LowMorale", "맹수의 본능 - 사기저하", "None", "None"),
            ("BeastEyes_Panic", "맹수의 본능 - 패닉", "None", "None"),
            ("Bothersome", "절참 [切斬]", "None", "None"),
            ("VibrationIgnition", "진동 - 작열", "None", "None"),
            ("BulletPropellant", "호표탄", "None", "None"),
            ("BulletPropellantSpecial", "맹호표탄", "None", "None"),
            ("BulletSpent", "BulletSpent", "None", "None"),
            ("Prey", "사냥감", "None", "None"),
            ("Complacency", "오버히트", "None", "None"),
            ("BattleSense", "저력 [底力]", "None", "None"),
            ("RestoredBattleSense", "극력 [極力]", "None", "None"),
            ("Irritation", "천퇴성[天退星]", "None", "None"),
            ("HugeIrritation", "신(心) - 천퇴성[天退星]", "None", "None"),
            ("LastingHongwonWill_LowMorale", "계승된 홍원의 뜻", "None", "None"),
            ("LastingHongwonWill_Panic", "계승된 홍원의 뜻", "None", "None"),
            ("PileObedient", "쌓이는 순종", "None", "None"),
            ("Obedient", "공허한 순종", "None", "None"),
            ("ExpensiveJade", "보옥요람", "None", "None"),
            ("FamilyTreasure", "폭발유희", "None", "None"),
            ("OutpouringAnger", "발악", "None", "None"),
            ("Honglu_Xi", "기쁨", "None", "None"),
            ("Honglu_Le", "기쁨, 즐거움", "None", "None"),
            ("Honglu_Ai", "기쁨, 즐거움, 슬픔", "None", "None"),
            ("Honglu_Nu", "기쁨, 즐거움, 슬픔, 분노", "None", "None"),
            ("SupremeEternalLife", "지고한 불사", "None", "None"),
            ("ImperfectEternalLife", "불완전한 불사", "None", "None"),
            ("FaintEternalLife", "FaintEternalLife", "None", "None"),
            ("LongLastingHongwonWill_LowMorale", "유구히 계승될 홍원의 뜻", "None", "None"),
            ("LongLastingHongwonWill_Panic", "유구히 계승될 홍원의 뜻", "None", "None"),
            ("GoldenBoughSync", "부감응", "None", "None"),
            ("GoldenBoughSyncDistorted", "괴력난신", "None", "None"),
            ("DisengageCombat", "전투 이탈", "None", "None"),
            ("ReloadKeepAmmo", "재장전 - 잔탄 유지", "None", "None"),
            ("MadFeather_LowMorale", "양날깃", "None", "None"),
            ("MadFeather_Panic", "양날깃", "None", "None"),
            ("MadFeather", "눈알 깃", "None", "None"),
            ("DeepAngry_LowMorale", "울분", "None", "None"),
            ("DeepAngry_Panic", "울분", "None", "None"),
            ("Chesed_Mercy", "헤세드의 자비", "None", "None"),
            ("Ryoshu_Attackup", "난도질당한 기억", "None", "None"),
            ("Honglu_EGOResourceup", "나의 마음과 생각...", "None", "None"),
            ("WaitingXichun", "대[待]", "None", "None"),
            ("StartXichun", "시[始]", "None", "None"),
            ("CheerUpXichun", "원[援]", "None", "None"),
            ("RetreatForCommon", "전장 퇴각", "None", "None"),
            ("BulletPropellantAlly", "호표탄", "None", "None"),
            ("BulletPropellantSpecialAlly", "맹호표탄", "None", "None"),
            ("IrritationAlly", "천퇴성[天退星]", "None", "None"),
            ("HugeIrritationAlly", "신(心) - 천퇴성[天退星]", "None", "None"),
            ("FireBulletPropellant", "작열 추진탄", "None", "None"),
            ("SingBulletSupport", "(엄지 싱클 탄환 보급 받는 대상 이펙트)", "None", "None"),
            ("BeastEyesAlly", "오버히트", "None", "None"),
            ("FellBulletPersonality", "흉탄", "None", "None"),
            ("Honglu_Xi_Mirror", "기쁨", "None", "None"),
            ("Honglu_Le_Mirror", "기쁨, 즐거움", "None", "None"),
            ("Honglu_Ai_Mirror", "기쁨, 즐거움, 슬픔", "None", "None"),
            ("Honglu_Nu_Mirror", "기쁨, 즐거움, 슬픔, 분노", "None", "None"),
            ("RoseThorn", "탐식극(貪食棘)", "None", "None"),
            ("ChoSuperCharge", "초아광축전", "None", "None"),
            ("ShareCharge", "자가 공진 회로", "None", "None"),
            ("WaveSinking", "역류", "None", "None"),
            ("HystericGauge", "히스테리", "None", "None"),
            ("MagicalGirlAppear", "마법소녀 등장!", "None", "None"),
            ("NoVillain", "역변-리버스드", "None", "None"),
            ("VillainMark", "악당 표식", "None", "None"),
            ("ArcanaQueenOfHate", "매지컬 아르카나", "None", "None"),
            ("ThePowerOfLoveAndHate", "사랑/증오", "None", "None"),
            ("ColdBlackTear", "차갑고 검은 눈물", "None", "None"),
            ("HelplessTear", "무력한 눈물", "None", "None"),
            ("WornOutKnight", "닳디 닳은 기사", "None", "None"),
            ("SignOfDespair", "절망의 가호", "None", "None"),
            ("CollapsedPride", "무너져 내린 긍지", "None", "None"),
            ("FailedToAssistQueen", "내가… 무슨 쓸모…야?", "None", "None"),
            ("UsedTooMuchPower", "힘을 너무 많이 썼나봐", "None", "None"),
            ("ChasingArcana", "영창 - 아르카나 슬레이브", "None", "None"),
            ("PowerOfLoveAndJustice", "사랑/정의", "None", "None"),
            ("CentralCommandTeamCaptain", "중앙본부팀 팀장", "None", "None"),
            ("BestWelfareTeamMember", "복지팀 우수 직원", "None", "None"),
            ("BlessingAlly", "가호", "None", "None"),
            ("ProtectiveSword", "지키는 검", "None", "None"),
            ("DespairAlly", "절망", "None", "None"),
            ("PenetratingSword", "꿰뚫는 검", "None", "None"),
            ("SwordCutwithTear", "눈물 벼리기", "None", "None"),
            ("MagicalGirlResponse", "마법소녀의 영창", "None", "None"),
            ("KnightBless", "가호", "None", "None"),
            ("TraumaShield", "트라우마 방지장", "None", "None"),
            ("BlackTearsAlly", "깊은 눈물", "None", "None"),
            ("ChasingArcanahard", "영창 - 아르카나 슬레이브", "None", "None"),
            ("UltraPrecisionTimeAcceleration", "초정밀 시간 가속", "None", "None"),
            ("OutisNoHat", "강력 징수 집행", "None", "None"),
            ("AccumulatedPastSinner", "축적된 과거", "None", "None"),
            ("HeishouDeathCount", "사중구활[死中求活]", "None", "None"),
            ("HeishouCombo", "존명", "None", "None"),
            ("HeishouComboCount", "몰아침", "None", "None"),
            ("HeishouAttack", "뜻에 따라, 베겠습니다.", "None", "None"),
            ("HeishouSupportProtect", "호위", "None", "None"),
            ("HeishouSupportProtectTypo", "HeishouSupportProtectTypo", "None", "None"),
            ("BurstHonglu", "BurstHonglu", "None", "None"),
            ("HeishouSynergy", "모든 흑수의 주인", "None", "None"),
            ("HeishouComboCountHonglu", "흑수환염[黑獣丸染]", "None", "None"),
            ("DarkHongluQiuAndHonglu", "패도를 걷는 자", "None", "None"),
            ("DarkHongluTeaching", "관철", "None", "None"),
            ("DarkHongluParryGahwan", "가증스럽군요, 형님.", "None", "None"),
            ("DarkHonglu_EGOResourceup", "수없이 많은 자들의 피를 흘리게 될지라도…", "None", "None"),
            ("DarkHonglu_Xi", "기쁨", "None", "None"),
            ("DarkHonglu_Le", "기쁨, 즐거움", "None", "None"),
            ("DarkHonglu_Ai", "기쁨, 즐거움, 슬픔", "None", "None"),
            ("DarkHonglu_Nu", "기쁨, 즐거움, 슬픔, 분노", "None", "None"),
            ("MD6Test101", "MD6Test101", "None", "None"),
            ("MD6Test102", "MD6Test102", "None", "None"),
            ("MD6Test103", "MD6Test103", "None", "None"),
            ("MD6Test104", "MD6Test104", "None", "None"),
            ("MD6Test105", "MD6Test105", "None", "None"),
            ("MD6Test106", "MD6Test106", "None", "None"),
            ("MD6Test107", "MD6Test107", "None", "None"),
            ("MD6Test108", "MD6Test108", "None", "None"),
            ("MD6Test111", "MD6Test111", "None", "None"),
            ("MD6Test112", "MD6Test112", "None", "None"),
            ("MD6Test113", "MD6Test113", "None", "None"),
            ("MD6Test114", "MD6Test114", "None", "None"),
            ("MD6Test115", "MD6Test115", "None", "None"),
            ("MD6Test116", "MD6Test116", "None", "None"),
            ("MD6Test117", "MD6Test117", "None", "None"),
            ("MD6Test118", "MD6Test118", "None", "None"),
            ("MD6LimitTest101", "MD6LimitTest101", "None", "None"),
            ("MD6LimitTest102", "MD6LimitTest102", "None", "None"),
            ("MD6LimitTest103", "MD6LimitTest103", "None", "None"),
            ("MD6LimitTest104", "MD6LimitTest104", "None", "None"),
            ("MD6LimitTest105", "MD6LimitTest105", "None", "None"),
            ("MD6LimitTest111", "MD6LimitTest111", "None", "None"),
            ("MD6LimitTest112", "MD6LimitTest112", "None", "None"),
            ("MD6LimitTest113", "MD6LimitTest113", "None", "None"),
            ("MD6LimitTest114", "MD6LimitTest114", "None", "None"),
            ("MD6LimitTest115", "MD6LimitTest115", "None", "None"),
            ("ChickenStance", "혈염 [血炎]", "None", "None"),
            ("LegStrengthHorseYisang", "각력【오】", "None", "None"),
            ("ConcussionYisang", "뇌진탕", "None", "None"),
            ("BreakthroughHorse", "적진 주파", "None", "None"),
            ("BattlefieldHorse", "호령", "None", "None"),
            ("LegStrengthHorse", "각력【오】", "None", "None"),
            ("ConcussionWei", "뇌진탕", "None", "None"),
            ("SweptAwayWei", "휩쓸림", "None", "None"),
            ("ReboundWei", "반동", "None", "None"),
            ("RunawayHorseWei_LowMorale", "고삐 풀린 말", "None", "None"),
            ("RunawayHorseWei_Panic", "고삐 풀린 말", "None", "None"),
            ("StudyStatSinclair_A", "알에서 나오기 위한 몸부림 Ⅰ", "None", "None"),
            ("StudyStatSinclair_B", "알에서 나오기 위한 몸부림 ⅠⅠ", "None", "None"),
            ("StudyStatSinclair", "알에서 나오기 위한 몸부림", "None", "None"),
            ("StudyStatSinclair_D", "알에서 나오기 위한 몸부림 ⅠⅤ", "None", "None"),
            ("StudyStatSinclair_E", "알에서 나오기 위한 몸부림 Ⅴ", "None", "None"),
            ("StudySignSinclair", "어느 표지", "None", "None"),
            ("SipOfAlcoholJin", "한 모금(眞)", "None", "None"),
            ("DrunkBlood", "혈주(血注)", "None", "None"),
            ("DrunkAwakening", "정신 각성", "None", "None"),
            ("DrunkDrifter_LowMorale", "취수야객", "None", "None"),
            ("DrunkDrifter_Panic", "취수야객", "None", "None"),
            ("NightDrifterGuilty", "미약한 죄책감", "None", "None"),
            ("StudyStatSinclair", "알에서 나오기 위한 몸부림", "None", "None"),
            ("CrazyWei", "광폭화", "None", "None"),
            ("SnakePoisonJin", "퍼지는 뱀 독", "None", "None"),
            ("SnakePoisonJinWeak", "뱀 독", "None", "None"),
            ("NervousImpairment", "신경 손상", "None", "None"),
            ("MlynarWaiting", "칼집 속 사람", "None", "None"),
            ("MlynarFury", "MlynarFury", "None", "None"),
            ("CuredFilm", "경화막", "None", "None"),
            ("GazeRyoshu", "시선", "None", "None"),
            ("ContemptRyoshu", "경멸", "None", "None"),
            ("VibrationBleeding", "진동 - 과다출혈", "None", "None"),
            ("NervousImpairmentEffect", "NervousImpairmentEffect", "None", "None"),
            ("UnfinishedDreamMirror", "물보다 진한 피에서 벗어나,", "None", "None"),
            ("UnfinishedDreamTwoMirror", "UnfinishedDreamTwoMirror", "None", "None"),
            ("FragmentOfHopeFamilyMirror", "내 아이의 동료들아", "None", "None"),
            ("FragmentOfHopeTwoFamilyMirror", "나를 대신해 그 아이의 꿈을 함께 해주려무나", "None", "None"),
            ("DerivativeHugeIrritation", "신(心) - 천퇴성[天退星]", "None", "None"),
            ("MD6101", "예리 XII", "None", "None"),
            ("MD6102", "강인함 XII", "None", "None"),
            ("MD6103", "육체 강화 XII", "None", "None"),
            ("MD6104", "성장 XII", "None", "None"),
            ("MD6105", "합 위력 증강 VII", "None", "None"),
            ("MD6106", "최종 위력 증강 VII", "None", "None"),
            ("MD6107", "기본 위력 증강 VII", "None", "None"),
            ("MD6111", "예리 XIII", "None", "None"),
            ("MD6112", "강인함 XIII", "None", "None"),
            ("MD6113", "육체 강화 XIII", "None", "None"),
            ("MD6114", "성장 XIII", "None", "None"),
            ("MD6115", "합 위력 증강 VIII", "None", "None"),
            ("MD6116", "최종 위력 증강 VIII", "None", "None"),
            ("MD6117", "기본 위력 증강 VIII", "None", "None"),
            ("MD6121", "예리 XIV", "None", "None"),
            ("MD6122", "강인함 XIV", "None", "None"),
            ("MD6123", "육체 강화 XIV", "None", "None"),
            ("MD6124", "성장 XIV", "None", "None"),
            ("MD6125", "합 위력 증강 IX", "None", "None"),
            ("MD6126", "최종 위력 증강 IX", "None", "None"),
            ("MD6127", "기본 위력 증강 IX", "None", "None"),
            ("MD6131", "예리 XV", "None", "None"),
            ("MD6132", "강인함 XV", "None", "None"),
            ("MD6133", "육체 강화 XV", "None", "None"),
            ("MD6134", "성장 XV", "None", "None"),
            ("MD6135", "합 위력 증강 X", "None", "None"),
            ("MD6136", "최종 위력 증강 X", "None", "None"),
            ("MD6137", "기본 위력 증강 X", "None", "None"),
            ("MD6141", "예리 XVI", "None", "None"),
            ("MD6142", "강인함 XVI", "None", "None"),
            ("MD6143", "육체 강화 XVI", "None", "None"),
            ("MD6144", "성장 XVI", "None", "None"),
            ("MD6145", "합 위력 증강 XI", "None", "None"),
            ("MD6146", "최종 위력 증강 XI", "None", "None"),
            ("MD6147", "기본 위력 증강 XI", "None", "None"),
            ("MD6LimitBaseN", "추가되는 제약", "None", "None"),
            ("MD6Limit101", "레벨 강화", "None", "None"),
            ("MD6Limit102", "쇠약", "None", "None"),
            ("MD6Limit103", "불꽃 낙인 I", "None", "None"),
            ("MD6Limit104", "인플레이션 I", "None", "None"),
            ("MD6Limit105", "자아 간섭 I", "None", "None"),
            ("MD6Limit111", "레벨 강화", "None", "None"),
            ("MD6Limit112", "쇠약", "None", "None"),
            ("MD6Limit113", "정신 착란 I", "None", "None"),
            ("MD6Limit114", "신경 촉진 I", "None", "None"),
            ("MD6Limit115", "정신력 고갈 I", "None", "None"),
            ("MD6Limit121", "레벨 강화", "None", "None"),
            ("MD6Limit122", "쇠약", "None", "None"),
            ("MD6Limit123", "진동 차단", "None", "None"),
            ("MD6Limit124", "인플레이션 II", "None", "None"),
            ("MD6Limit125", "자아 간섭 II", "None", "None"),
            ("MD6Limit131", "레벨 강화", "None", "None"),
            ("MD6Limit132", "쇠약", "None", "None"),
            ("MD6Limit133", "생명력 증폭", "None", "None"),
            ("MD6Limit134", "파괴력", "None", "None"),
            ("MD6Limit135", "불꽃 낙인 II", "None", "None"),
            ("MD6Limit141", "레벨 강화", "None", "None"),
            ("MD6Limit142", "쇠약", "None", "None"),
            ("MD6Limit143", "정신 착란 II", "None", "None"),
            ("MD6Limit144", "신경 촉진 II", "None", "None"),
            ("MD6Limit145", "정신력 고갈 II", "None", "None"),
            ("GiveMeCandy", "사탕", "None", "None"),
            ("HungryCharon", "배고픔-분노", "None", "None"),
            ("FullCharon", "포만감", "None", "None"),
            ("GiveMeCandy_LowMorale", "사탕", "None", "None"),
            ("GiveMeCandy_Panic", "사탕", "None", "None"),
            ("VerEmergencyCandy", "비상용 사탕", "None", "None"),
            ("VerHunger", "배고픔", "None", "None"),
            ("VerShiningFullness", "빛나는 포만감", "None", "None"),
            ("DampSwordCase", "DampSwordCase", "None", "None"),
            ("BrokenSwordCase", "BrokenSwordCase", "None", "None"),
            ("Cianjing", "시엔징", "None", "None"),
            ("SeniorCianjing", "상급 시엔징", "None", "None"),
            ("SeaTerrorCell", "시테러 동화", "None", "None"),
            ("NetherseaBrand", "명흔", "None", "None"),
            ("CallOfSea_LowMorale", "동족의 부름", "None", "None"),
            ("CallOfSea_Panic", "동족의 부름", "None", "None"),
            ("WaitingforCommandMod", "명령 대기", "None", "None"),
            ("ParticipationMod", "참전", "None", "None"),
            ("AbyssalVitality", "깊은 바다의 생명력", "None", "None"),
            ("IndelibleGoodwill", "지워지지 않을 선의", "None", "None"),
            ("DesireToSave_LowMorale", "선의의 마음", "None", "None"),
            ("DesireToSave_Panic", "선의의 마음", "None", "None"),
            ("DOQ_LowMorale", "군체 의식", "None", "None"),
            ("DOQ_Panic", "군체 의식", "None", "None"),
            ("ColdAirDOQ", "냉기", "None", "None"),
            ("FreezingDOQ", "빙결", "None", "None"),
            ("BloodborneDOQ", "기사의 피와 뼈", "None", "None"),
            ("WeakenDOQ", "재난과 시련", "None", "None"),
            ("BitOfEmotion", "감정 공유", "None", "None"),
            ("WildernessSurvivalModule", "조력자 - 야외 생존 유닛", "None", "None"),
            ("HousekeepingAssistantModule", "조력자 - 가사 보조 모듈", "None", "None"),
            ("IncrementalMotionFirmware", "조력자 - 운동 증량 펌웨어", "None", "None"),
            ("MiniDecontaminationKit", "조력자 - 얼룩 제거 세트", "None", "None"),
            ("ExternalUpgradeModule", "조력자 - 외장 강화 장치", "None", "None"),
            ("NetherseaBrandUnit", "명흔", "None", "None"),
            ("CommonExit", "전장 이탈", "None", "None"),
            ("RageResonance", "움켜쥔 분노", "None", "None"),
            ("ArrowShiFau", "화살 - 시", "None", "None"),
            ("ArrowInTheEyeFau", "박힌 화살", "None", "None"),
            ("AimForTheGoal", "목표 조준", "None", "None"),
            ("SnipingArrowMode", "저격 자세", "None", "None"),
            ("AStrokeOfDeath", "절명", "None", "None"),
            ("WOverCharge", "과충전", "None", "None"),
            ("BloodArmorMeursault", "경혈 갑주", "None", "None"),
            ("FocusOnActing", "연기 집중", "None", "None"),
            ("ParadeConcentration", "라만차 퍼레이드", "None", "None"),
            ("BloodArmorMeursaultDrainEffect", "BloodArmorMeursaultDrainEffect", "None", "None"),
            ("EmergencyChargeForceField", "비상용 역장 배터리", "None", "None"),
            ("BloodArmorCasting", "주조된 경혈", "None", "None"),
            ("HanafudaOne", "짝패 - 송학", "None", "None"),
            ("HanafudaTwo", "짝패 - 억새", "None", "None"),
            ("HanafudaThree", "짝패 - 청벚꽃", "None", "None"),
            ("HanafudaCombo", "광【光】", "None", "None"),
            ("HurtNightStiletto", "상처", "None", "None"),
            ("CriHurtNightStiletto", "깊은 상처", "None", "None"),
            ("HorrHurtNightStiletto", "치명적인 상처", "None", "None"),
            ("GrownHorns", "발각[發角]", "None", "None"),
            ("CrushMarks", "파쇄흔", "None", "None"),
            ("CrushMarks_Main", "파쇄흔", "None", "None"),
            ("CrushMarks_Sub", "파쇄흔", "None", "None"),
            ("CrushMarks_LowMorale", "파쇄흔", "None", "None"),
            ("CrushMarks_Panic", "파쇄흔", "None", "None"),
            ("AggressiveBokgak", "본능", "None", "None"),
            ("DeadEGOResource", "남겨진 의지", "None", "None"),
            ("ProtectStanceRyoshu", "호위태세", "None", "None"),
            ("ChickenFightlust", "혈투본능", "None", "None"),
            ("HowDareYouApple", "감히…!", "None", "None"),
            ("RapidGrowthApple", "급한 성장", "None", "None"),
            ("ObservationMoses", "관찰력", "None", "None"),
            ("CalmAnalysisMoses", "차분한 분석", "None", "None"),
            ("MuscleContraction", "근섬유의 기계적 수축과 이완", "None", "None"),
            ("DuelEdge", "결투 고조", "None", "None"),
            ("HostageCharon", "인질극", "None", "None"),
            ("DicipleLittleFinger", "월하청도", "None", "None"),
            ("HumanFleshTheRings", "생체 재료", "None", "None"),
            ("BoneBladeTheRings", "작품명: 파시아", "None", "None"),
            ("UnsteadyTheRings", "심하게 울리는 갑주", "None", "None"),
            ("BreathLoss_LowMorale", "흐트러진 숨", "None", "None"),
            ("BreathLoss_Panic", "흐트러진 숨", "None", "None"),
            ("IronMaiden_LowMorale", "아이언 메이든", "None", "None"),
            ("IronMaiden_Panic", "아이언 메이든", "None", "None"),
            ("RyoshuSlotPlusA1C9", "과거의 악취", "None", "None"),
            ("ElseRyoshuSlotPlusA1C9", "ElseRyoshuSlotPlusA1C9", "None", "None"),
            ("IndexPrescriptTargetMarkToEnemy", "지령 표식", "None", "None"),
            ("IndexPrescript_Base", "지령", "None", "None"),
            ("IndexPrescriptFaust_0", "지령[쪽지] I", "None", "None"),
            ("IndexPrescriptFaust_1", "지령[쪽지] II", "None", "None"),
            ("IndexPrescriptFaust_2", "지령[쪽지] III", "None", "None"),
            ("IndexPrescriptFaust_3", "지령[쪽지] IV", "None", "None"),
            ("BlessingOfIndexPrescriptAlly", "지령의 가호", "None", "None"),
            ("KarmaOfIndexAlly", "카르마", "None", "None"),
            ("TeachersPet_LowMorale", "교본", "None", "None"),
            ("TeachersPet_Panic", "교본", "None", "None"),
            ("SeveredTendon", "힘줄 절단", "None", "None"),
            ("TheUdjatOutis", "우제트의 눈 [선봉]", "None", "None"),
            ("SheutFracture", "셰우트의 균열", "None", "None"),
            ("BlueSand", "푸른 모래", "None", "None"),
            ("BlueSand_Main", "푸른 모래", "None", "None"),
            ("BlueSand_Sub", "푸른 모래", "None", "None"),
            ("BlueSand_LowMorale", "푸른 모래", "None", "None"),
            ("BlueSand_Panic", "푸른 모래", "None", "None"),
            ("IndexPrescriptTargetToEnemy", "지령 대상", "None", "None"),
            ("LCA_Bullet", "LCA 균열탄", "None", "None"),
            ("IndexCommonUnlock_1", "IndexCommonUnlock_1", "None", "None"),
            ("IndexCommonUnlock_2", "IndexCommonUnlock_2", "None", "None"),
            ("IndexCommonUnlock_3", "IndexCommonUnlock_3", "None", "None"),
            ("IndexCommonUnlock_4", "IndexCommonUnlock_4", "None", "None"),
            ("IndexCommonPrescriptTarget", "IndexCommonPrescriptTarget", "None", "None"),
            ("IndexCommonBlessing", "IndexCommonBlessing", "None", "None"),
            ("IndexFaustMissionEffect", "IndexFaustMissionEffect", "None", "None"),
            ("SheutFractureMaxStackEffect", "SheutFractureMaxStackEffect", "None", "None"),
            ("IDMicroChip", "주민등록 마이크로칩", "None", "None"),
            ("NiddleEGO", "바늘", "None", "None"),
            ("Vespa_LowMorale", "언짢음", "None", "None"),
            ("Vespa_Panic", "언짢음", "None", "None"),
            ("LCEFireFly_LowMorale", "열화침식", "None", "None"),
            ("LCEFireFly_Panic", "열화침식", "None", "None"),
            ("YellowHarpoon", "노란작살", "None", "None"),
            ("YellowHarpoonSin", "신(心) - 노란작살", "None", "None"),
            ("BeeSting", "박힌 작살 [벌침]", "None", "None"),
            ("ArtifactBeeSting", "박힌 작살 [말벌침]", "None", "None"),
            ("HornetPoison", "궁니르 공명", "None", "None"),
            ("CriticalDmgUpVespa", "섬봉광검술 - 환도", "None", "None"),
            ("IndexPrescriptStudent_0", "지령[단말기] I", "None", "None"),
            ("IndexPrescriptStudent_1", "지령[단말기] II", "None", "None"),
            ("IndexPrescriptStudent_2", "지령[단말기] III", "None", "None"),
            ("IndexPrescriptStudent_3", "지령[단말기] IV", "None", "None"),
            ("IndexPrescriptTargetToPersonality", "검지의 지령 대상", "None", "None"),
            ("Blandishment_Enemy", "당연한 믿음", "None", "None"),
            ("UnlockBuff_Base", "해금", "None", "None"),
            ("UnlockBuff_1", "해금 - I", "None", "None"),
            ("UnlockBuff_2", "해금 - II", "None", "None"),
            ("UnlockBuff_3", "해금 - III", "None", "None"),
            ("PressureOfPrescript", "압박감", "None", "None"),
            ("PressureOfPrescript_2nd", "극심한 압박감", "None", "None"),
            ("KarmaOfIndexStudent", "카르마", "None", "None"),
            ("Ryoshu_IndexFightBuff", "난도질당한 기억과 추억", "None", "None"),
            ("ScarBygoneDays_LowMorale", "애증", "None", "None"),
            ("ScarBygoneDays_Panic", "애증", "None", "None"),
            ("PhantomIncision", "검흔[잔상]", "None", "None"),
            ("PhantomIncisionTotal", "지혜성도", "None", "None"),
            ("LvDownLittleFingerBoss", "관망", "None", "None"),
            ("TimeGapAb", "괴리", "None", "None"),
            ("TimeEntangleFour", "4중 잔상 얽힘", "None", "None"),
            ("TimeEntangleThree", "3중 잔상 얽힘", "None", "None"),
            ("TimeEntangleTwo", "2중 잔상 얽힘", "None", "None"),
            ("TimeEntangleOne", "1중 잔상 얽힘", "None", "None"),
            ("LittleFingerBoss_Shin", "신(心) - 지혜성", "None", "None"),
            ("BlessingOfIndexPrescriptEnemy", "지령의 가호", "None", "None"),
            ("BlindFaith_LowMorale", "지령 붕괴 위기", "None", "None"),
            ("BlindFaith_Panic", "지령 붕괴 위기", "None", "None"),
            ("IndexPrescript_Base_2nd", "지령", "None", "None"),
            ("IndexPrescriptDon_0", "지령[단말기] I", "None", "None"),
            ("IndexPrescriptDon_1", "지령[단말기] II", "None", "None"),
            ("IndexPrescriptDon_2", "지령[단말기] III", "None", "None"),
            ("IndexPrescriptDon_3", "지령[단말기] IV", "None", "None"),
            ("UnlockBuffAlly_1", "해금 - I", "None", "None"),
            ("UnlockBuffAlly_2", "해금 - II", "None", "None"),
            ("UnlockBuffAlly_3", "해금 - III", "None", "None"),
            ("Blandishment", "당연한 믿음", "None", "None"),
            ("Blandishment_Shin", "신(心) - 대행", "None", "None"),
            ("LvUpFireFly", "E.G.O 감응도 증가", "None", "None"),
            ("IndexPrescript_0", "IndexPrescript_0", "None", "None"),
            ("IndexPrescript_1", "IndexPrescript_1", "None", "None"),
            ("IndexPrescript_2", "IndexPrescript_2", "None", "None"),
            ("IndexPrescript_3", "IndexPrescript_3", "None", "None"),
            ("KarmaOfIndexEnemy", "카르마", "None", "None"),
            ("Cubism_LowMorale", "한물간 예술", "None", "None"),
            ("Cubism_Panic", "한물간 예술", "None", "None"),
            ("LanternHohenheimBigBird", "램프", "None", "None"),
            ("DelusionHohenheimBigBird", "현혹", "None", "None"),
            ("LvUpHohenheimBigBird", "E.G.O 감응도 증가", "Neutral", "NonvolatileBuff"),
            ("HohenheimBigBird_LowMorale", "쏟아지는 잠", "None", "None"),
            ("HohenheimBigBird_Panic", "쏟아지는 잠", "None", "None"),
            ("HumanBoneTheRings", "생체 재료(뼈)", "None", "None"),
            ("HumanBloodTheRings", "생체 재료(피)", "None", "None"),
            ("LivingSpecimenTheRings", "신체관극", "None", "None"),
            ("MineTheRings", "작품명: 티비아", "None", "None"),
            ("ObservationTheRings", "조망", "None", "None"),
            ("MosesWhiteBreathMental", "하얀 숨 - 정신 보호", "None", "None"),
            ("MosesRedBreathBody", "붉은 숨 - 육체 강화", "None", "None"),
            ("MosesRedBreathShield", "붉은 숨 - 보호", "None", "None"),
            ("MosesPurpleBreathBind", "보라색 숨 - 히스테릭", "None", "None"),
            ("MosesRedBreathCharge", "깊은 숨 - 붉은색", "None", "None"),
            ("MosesSin", "신(心) - 모제스", "None", "None"),
            ("MosesMiddle_LowMorale", "돌이킬 수 없이 망가진 몸", "None", "None"),
            ("MosesMiddle_Panic", "돌이킬 수 없이 망가진 몸", "None", "None"),
            ("VengeanceBookSpider", "앙갚음 장부", "None", "None"),
            ("ResentmentSpider", "중지 - 원한", "None", "None"),
            ("GotACompliment", "칭찬 받았다!", "None", "None"),
            ("DaughterEducationSpider", "잘 봐둬라 딸!", "None", "None"),
            ("ReinforcedTattooSpider", "중지 - 원한 문신 [<s>큰 형님</s>]", "None", "None"),
            ("HeatingWireOnSpider", "열선 ON", "None", "None"),
            ("HeatingWireOffSpider", "열선 OFF", "None", "None"),
            ("MiddleFatherGuts", "초근성", "None", "None"),
            ("MiddleFingerDaddy_LowMorale", "광분", "None", "None"),
            ("MiddleFingerDaddy_Panic", "광분", "None", "None"),
            ("MiddleFingerDaughter_LowMorale", "흥분", "None", "None"),
            ("MiddleFingerDaughter_Panic", "흥분", "None", "None"),
            ("ResolveRyoshu", "ResolveRyoshu", "None", "None"),
            ("CutoffRyoshu", "절사 [切絲]", "None", "None"),
            ("CutbondRyoshu", "무아 [無我]", "None", "None"),
            ("DukkhaRyoshu_LowMorale", "DukkhaRyoshu_LowMorale", "None", "None"),
            ("DukkhaRyoshu_Panic", "DukkhaRyoshu_Panic", "None", "None"),
            ("OffenseBug", "공격 해충[바퀴]", "None", "None"),
            ("DefenseBug", "방어 해충[바퀴]", "None", "None"),
            ("AgainstMyWill_LowMorale", "‘나, 날… 죽여줘..’", "None", "None"),
            ("AgainstMyWill_Panic", "‘나, 날… 죽여줘..’", "None", "None"),
            ("AaCfPcBiv2", "고열기전", "None", "None"),
            ("VergiliusSin", "신(心) - 붉은시선", "None", "None"),
            ("BloodBranch", "망혈[盲血]", "None", "None"),
            ("SpecimenTheRings", "전시회의 완성", "None", "None"),
            ("KAmpouleA1C9", "KAmpouleA1C9", "None", "None"),
            ("RyoshuEGOPF", "나생[羅生]", "None", "None"),
            ("EsteemNeeds", "인정 욕구", "None", "None"),
            ("Fugacious", "검의 길", "None", "None"),
            ("LvUpHohenheimBigBird_Over", "E.G.O 감응도 과잉", "None", "None"),
            ("LvUpFireFly_Over", "E.G.O 감응도 과잉", "None", "None"),
            ("DimensionalBagEzraA", "차원가방 - 알라스 공방", "None", "None"),
            ("DimensionalBagEzraB", "차원가방 - 네스터 공방", "None", "None"),
            ("DimensionalBagEzraC", "차원가방 - 스크류 아틀리에", "None", "None"),
            ("DimensionalBagEzraD", "차원가방 - 나미르 공방", "None", "None"),
            ("DimensionalBagEzraE", "차원가방 - 유리아 공방", "None", "None"),
            ("MiddleFingerEzraSin", "신(心) - 에즈라", "None", "None"),
            ("EzraMiddle_LowMorale", "자기 방어 - 가면", "None", "None"),
            ("EzraMiddle_Panic", "자기 방어 - 가면", "None", "None"),
            ("Gebura_Reinforcement", "게부라의 칼날", "None", "None"),
            ("ObsessedTeacher_LowMorale", "가문의 수치", "None", "None"),
            ("ObsessedTeacher_Panic", "가문의 수치", "None", "None"),
            ("FutureEyeOn", "예지안", "None", "None"),
            ("FutureEyeOff", "예지안 과열", "None", "None"),
            ("LookingFuture", "가속하는 미래", "None", "None"),
            ("ObsessionAndGreed", "신(心) - 발렌치나", "None", "None"),
            ("AccelBullet", "가속탄", "None", "None"),
            ("Sword2ndTheRings", "Sword2ndTheRings", "None", "None"),
            ("RedemptionTheRings", "재료가 떨어지는 상처", "None", "None"),
            ("MosesWarPTSD", "그 날의 기억", "None", "None"),
            ("ThreeMirrorpartYiSang", "깨어진 세계", "None", "None"),
            ("WillAwakenSinclair", "신(心) - 어느 싱클레어", "None", "None"),
            ("SignAwakenSinclair", "표지 - 미래현현", "None", "None"),
            ("Veteran_LowMorale", "제 2차 연기전쟁의", "None", "None"),
            ("Veteran_Panic", "제 2차 연기전쟁의", "None", "None"),
            ("IndexPrescriptRien_0", "지령[단말기] I", "None", "None"),
            ("IndexPrescriptRien_1", "지령[단말기] II", "None", "None"),
            ("IndexPrescriptRien_2", "지령[단말기] III", "None", "None"),
            ("IndexPrescriptRien_3", "지령[단말기] IV", "None", "None"),
            ("IndexPrescript_RienSecondPhase", "지령[단말기] - 경고", "None", "None"),
            ("KarmaOfIndexRien", "카르마", "None", "None"),
            ("FanaticismRien", "FanaticismRien", "None", "None"),
            ("Shin_Rien", "신(心) - 뤼엔", "None", "None"),
            ("PainfulScar_LowMorale", "모조된 아비", "None", "None"),
            ("PainfulScar_Panic", "모조된 아비", "None", "None"),
            ("Ryoshu_RienBattle_1Phase", "케츠엔 [けつえん]", "None", "None"),
            ("Ryoshu_RienBattle_2Phase", "렌케츠엔 [れんけつえん]", "None", "None"),
            ("SojiAbiBuffOne", "계명(雞鳴)", "None", "None"),
            ("SojiAbiBuffTwo", "일입(日入)", "None", "None"),
            ("SojiAbiBuffThree", "황혼(黃昏)", "None", "None"),
            ("SojiAbiBuffFour", "SojiAbiBuffFour", "None", "None"),
            ("TeachersCommand", "엄지 아비의 명령", "None", "None"),
            ("RyoshuStartA1C9", "되살아나는 희열", "None", "None"),
            ("RyoshuParrySoji", "뒤엉키고 틀어진 기억과 추억", "None", "None"),
            ("Blandishment_Shin_Enemy", "신(心) - 소라", "None", "None"),
            ("RyoshuStartA1C9P2", "난도질당한 기억", "None", "None"),
            ("RyoshuParrySojiTwo", "끊어지지 않은 기억", "None", "None"),
            ("ResentmentSpiderDaughter", "중지 - 원한", "None", "None"),
            ("ReinforcedTattooSpiderDaughter", "중지식 강화 문신", "None", "None"),
            ("LimitedAwakenSinclair", "움브라 금제 해제", "None", "None"),
            ("HeavenlyKillerStar", "천살성도 - 아라야시키 [天殺星刀阿賴耶識]", "None", "None"),
            ("TeachersPrey", "사냥 표적", "None", "None"),
            ("LvDownLittleFingerBossTwo", "천살성상[天殺星傷]", "None", "None"),
            ("SojiAbiAgePast", "역행 - 미움", "None", "None"),
            ("SojiAbiAgeFuture", "순행 - 연민", "None", "None"),
            ("TimeEntangleUnstable", "폭주 - 잔상 얽힘", "None", "None"),
            ("RyoshuParrySojiThree", "끊어지지 않는 시간", "None", "None"),
            ("MiddleFatherSwordOne", "봉인된 검", "None", "None"),
            ("MiddleFatherSwordTwo", "1단계 봉인 해제", "None", "None"),
            ("MiddleFatherSwordThree", "2단계 봉인 해제", "None", "None"),
            ("MiddleFatherSwordFour", "레바테인", "None", "None"),
            ("Malkuth_Imperfect", "말쿠트의 통치 - 불완전 현현", "None", "None"),
            ("KAmpouleA1C92ND", "KAmpouleA1C92ND", "None", "None"),
            ("PhantomIncisionSuccessEffect", "PhantomIncisionSuccessEffect", "None", "None"),
            ("BurningWoundRien_Mask", "뤼엔의 가면", "None", "None"),
            ("BurningWoundRien", "이글거리는 상처", "None", "None"),
            ("StackRienSpecialSkill", "대행 [헤르메스]", "None", "None"),
            ("KAmpouleA1C93RD", "KAmpouleA1C93RD", "None", "None"),
            ("KAmpouleA1C94TH", "비상용 K사 앰플", "None", "None"),
            ("TimeEntangleDesc", "잔상 얽힘", "None", "None"),
            ("CutoffRyoshuOnDie", "CutoffRyoshuOnDie", "None", "None"),
            ("MosesRedBreathAttack", "붉은 숨 - 내달리는 몸", "None", "None"),
            ("KAA1C9ActivateEffect", "K사 앰플 사용", "None", "None"),
            ("RienWeaponBase", "RienWeaponBase", "None", "None"),
            ("RienWeapon01Hatchet", "손도끼로 갈비뼈를 찍어 내릴 때는…", "None", "None"),
            ("RienWeapon01Hatchet2phase", "손도끼로 갈비뼈를 찍어 내릴 때는…", "None", "None"),
            ("RienWeapon02Stiletto", "스틸레토로 허파를 꿰뚫을 때는…", "None", "None"),
            ("RienWeapon02Stiletto2phase", "스틸레토로 허파를 꿰뚫을 때는…", "None", "None"),
            ("RienWeapon03Greatsword", "바스타드 소드로 어깨와 머리를 짓이길 때는…", "None", "None"),
            ("RienWeapon03Greatsword2phase", "바스타드 소드로 어깨와 머리를 짓이길 때는…", "None", "None"),
            ("RienWeapon04Rapier", "레이피어로 몸에 10개 이상의 구멍을 내야할 때는…", "None", "None"),
            ("RienWeapon04Rapier2phase", "레이피어로 몸에 10개 이상의 구멍을 내야할 때는…", "None", "None"),
            ("RienWeapon05Sledgehammer", "망치로 뒤통수를 으깨야 할 때는…", "None", "None"),
            ("RienWeapon05Sledgehammer2phase", "망치로 뒤통수를 으깨야 할 때는…", "None", "None"),
            ("RienWeapon06Ultragreatsword", "커다란 검으로 몸통을 갈라야 할 때는…", "None", "None"),
            ("RienWeapon06Ultragreatsword2phase", "커다란 검으로 몸통을 갈라야 할 때는…", "None", "None"),
            ("RienWeapon07Lance", "랜스로 20인치의 구멍을 내야 할 때는…", "None", "None"),
            ("RienWeapon07Lance2phase", "랜스로 20인치의 구멍을 내야 할 때는…", "None", "None"),
            ("RienWeapon08Chain", "채찍으로 살점을 만 갈래 떼어내야 할 때는…", "None", "None"),
            ("RienWeapon08Chain2phase", "채찍으로 살점을 만 갈래 떼어내야 할 때는…", "None", "None"),
            ("RienWeapon09Scythe", "낫으로… 누군가처럼 공간을 따라 베어내야 할 때는…", "None", "None"),
            ("RienWeapon09Scythe2phase", "낫으로… 누군가처럼 공간을 따라 베어내야 할 때는…", "None", "None"),
            ("DefenseBugEffect", "DefenseBugEffect", "None", "None"),
            ("RyoshuParrySojiWe", "붉은 실", "None", "None"),
            ("RyoshuParrySojiThey", "붉은 실", "None", "None"),
            ("UnlockBuff_Rien1", "해금 - I", "None", "None"),
            ("UnlockBuff_Rien2", "해금 - II", "None", "None"),
            ("UnlockBuff_Rien3", "해금 - III", "None", "None"),
            ("KarmaOfIndexRien_2Phase", "카르마 [포르투나]", "None", "None"),
            ("OffenseBugEffect", "OffenseBugEffect", "None", "None"),
            ("CriticalDamageUp", "크리티컬 피해량 증가", "None", "None"),
            ("BlackNightmare", "지령 탐닉", "None", "None"),
            ("RyoshuParryIndexFingerWe", "붉은 실", "None", "None"),
            ("RyoshuParryIndexFingerThey", "붉은 실", "None", "None"),
            ("ReinforcedTattooIshmael", "중지식 강화 문신", "None", "None"),
            ("ResentmentIshmael", "중지 - 원한", "None", "None"),
            ("HeatingWireIshmael", "열선", "None", "None"),
            ("LanternGregBigBird", "램프", "None", "None"),
            ("ObedienceGregBigBird", "숲의 파수꾼", "None", "None"),
            ("VigilanceGregBigBird", "VigilanceGregBigBird", "None", "None"),
            ("DelusionGregBigBird", "현혹", "None", "None"),
            ("DelusionGregBigBird_LowMorale", "현혹", "None", "None"),
            ("DelusionGregBigBird_Panic", "현혹", "None", "None"),
            ("DelusionGregBigBird_Main", "현혹", "None", "None"),
            ("DelusionGregBigBird_Sub", "현혹", "None", "None"),
            ("ExtractCoin", "적출 코인", "None", "None"),
            ("LittleFingerID", "월하청도", "None", "None"),
            ("IndexPrescriptYi_0", "지령[단말기] I", "None", "None"),
            ("IndexPrescriptYi_1", "지령[단말기] II", "None", "None"),
            ("IndexPrescriptYi_2", "지령[단말기] III", "None", "None"),
            ("IndexPrescriptYi_3", "지령[단말기] IV", "None", "None"),
            ("SatisfyingEsteemNeeds", "인정 욕구 충족", "None", "None"),
            ("StackYisangSpecialSkill", "대행 [헤르메스]", "None", "None"),
            ("Shin_IndexFingerYisang", "신(心) - 운명", "None", "None"),
            ("BurningWoundYisangMask", "상처를 가린 가면", "None", "None"),
            ("BurningWoundYisang", "이글거리는 상처", "None", "None"),
            ("BlackNightmareYisang", "지령 탐닉", "None", "None"),
            ("YisangWeapon01Hatchet", "손도끼로 갈비뼈를 찍어 내릴 때는…", "None", "None"),
            ("YisangWeapon02Stiletto", "스틸레토로 허파를 꿰뚫을 때는…", "None", "None"),
            ("YisangWeapon03Greatsword", "바스타드 소드로 어깨와 머리를 짓이길 때는…", "None", "None"),
            ("YisangWeapon04Rapier", "레이피어로 몸에 10개 이상의 구멍을 내야할 때는…", "None", "None"),
            ("YisangWeapon05Sledgehammer", "망치로 뒤통수를 으깨야 할 때는…", "None", "None"),
            ("YisangWeapon06Ultragreatsword", "커다란 검으로 몸통을 갈라야 할 때는…", "None", "None"),
            ("YisangWeapon07Lance", "랜스로 20인치의 구멍을 내야 할 때는…", "None", "None"),
            ("YisangWeapon08Chain", "채찍으로 살점을 만 갈래 떼어내야 할 때는…", "None", "None"),
            ("YisangWeapon09Scythe", "낫으로… 누군가처럼 공간을 따라 베어내야 할 때는…", "None", "None"),
            ("YisangWeaponBase", "YisangWeaponBase", "None", "None"),
            ("ObjectOfExploration", "탐구 대상", "None", "None"),
            ("Inspire", "영감", "None", "None"),
            ("PaintingMaterial", "그림 재료", "None", "None"),
            ("GreatAesthetics", "훌륭한 미감", "None", "None"),
            ("GiftCannon", "화력", "None", "None"),
            ("GiftGlass", "연약", "None", "None"),
            ("EnhanceRoseSign", "굴레", "None", "None"),
            ("BlandishmentShinEnemyDelete", "신(心) - ???", "None", "None"),
            ("LeakedOutSauce", "새어나온 양념장", "None", "None"),
            ("MeursaultBeeSpore", "포자", "None", "None"),
            ("MeursaultWorkerBee", "충성 페로몬", "None", "None"),
            ("MeursaultSporeBulletLong", "포자탄[기본]", "None", "None"),
            ("MeursaultSporeBulletShort", "포자탄[산탄]", "None", "None"),
            ("MeursaultSporeBulletReloading", "재장전[포자보충]", "None", "None"),
            ("ThatIsRhythm", "리듬", "None", "None"),
            ("BulletGodok", "탄환 - 고독", "None", "None"),
            ("AlriuneEGOThey", "잔향", "None", "None"),
            ("AlriuneEGOWe", "꽃잎", "None", "None"),
            ("GodokPanicType", "고독", "None", "None"),
            ("Godok_Lowmorale", "고독", "None", "None"),
            ("Godok_Panic", "고독", "None", "None"),
            ("GodokPanicType_Main", "고독", "None", "None"),
            ("GodokPanicType_Sub", "고독", "None", "None"),
            ("BestWelfareTeamCaptain", "복지팀 팀장", "None", "None"),
            ("RunHallway", "무리 이동 준비", "None", "None"),
            ("NextHallway", "무리 이주", "None", "None"),
            ("PumpkinJelly", "오렌지맛 뀽뀽이", "None", "None"),
            ("DesperadoBuff", "데스페라도", "None", "None"),
            ("TensionUp", "텐션 업", "None", "None"),
            ("PinkPetals", "분홍 꽃잎", "None", "None"),
            ("QueenBeepheromone", "페로몬", "None", "None"),
            ("QueenBeeMark", "페로몬 표식", "None", "None"),
            ("MeursaultBeeGunLong", "호넷[라이플]", "None", "None"),
            ("MeursaultBeeGunShort", "호넷[샷건]", "None", "None"),
            ("AddBullet", "AddBullet", "None", "None"),
            ("CommonReload", "CommonReload", "None", "None"),
            ("PhotoElectricityFlux_Dummy", "PhotoElectricityFlux_Dummy", "None", "None"),
            ("SelfChargeAlly", "자가 충전", "None", "None"),
            ("HighVoltageExoshell", "고전압 외피", "None", "None"),
            ("ChargedSting", "전하침", "None", "None"),
            ("FaustFlameMothEmber", "잔불", "None", "None"),
            ("BloodthirstHard", "피갈망", "None", "None"),
            ("DeliciousSauce", "황금 비법 소스", "None", "None"),
            ("Yummy_Lowmorale", "맛있어짐", "None", "None"),
            ("Yummy_Panic", "맛있어짐", "None", "None"),
            ("ArtworkBodyArt01", "ArtworkBodyArt01", "None", "None"),
            ("ArtworkBodyArt02", "ArtworkBodyArt02", "None", "None"),
            ("ArtworkBodyArt03", "ArtworkBodyArt03", "None", "None"),
            ("TouchBodyArt01", "TouchBodyArt01", "None", "None"),
            ("ChargeBodyArt", "생체 재료", "None", "None"),
            ("TibiaPersonality", "작품명: 티비아", "None", "None"),
            ("MelodyBodyArt", "신체가 울리는 선율", "None", "None"),
            ("IronMaidenPersonality", "아이언 메이든", "None", "None"),
            ("SilverOpportunity", "구속 해제 - 창작 몰입", "None", "None"),
            ("FasciaPersonality", "작품명: 파시아", "None", "None"),
            ("ArtworkBodyArt01Effect", "ArtworkBodyArt01Effect", "None", "None"),
            ("ArtworkBodyArt02Effect", "ArtworkBodyArt02Effect", "None", "None"),
            ("ArtworkBodyArt03Effect", "ArtworkBodyArt03Effect", "None", "None"),
            ("LivingSpecimenPersonality", "신체관극", "None", "None"),
            ("GoldenOpportunity", "조망", "None", "None"),
            ("MelodyBodyArtChebello", "신체가 울리는 선율[강화됨]", "None", "None"),
            ("MiddleFatherGutsMirror", "초근성", "None", "None"),
            ("TeachersPreyMirror", "사냥 표적", "None", "None"),
            ("ObservationTheRingsHidden", "조망", "None", "None"),
            ("LookingFutureMirror", "순간의 예지", "None", "None"),
            ("FutureEyeOnMirror", "예지안", "None", "None"),
            ("FutureEyeOffMirror", "예지안 과열", "None", "None"),
            ("MD7LimitBaseN", "MD7LimitBaseN", "None", "None"),
            ("MD7Limit101", "전력 재분배", "None", "None"),
            ("MD7Limit111", "정신적 고양", "None", "None"),
            ("MD7Limit121", "보호막 생성", "None", "None"),
            ("MD7Limit131", "균열 증식", "None", "None"),
            ("MD7Limit141", "상태이상 부여", "None", "None"),
            ("BearClawWound", "찢긴 색채 [루주]", "None", "None"),
            ("EagleClawWound", "찢긴 색채 [블뢰]", "None", "None"),
            ("FaubismWolfMaskRodion", "야수파 - 마스크 드 루", "None", "None"),
            ("BoldBrushstrokesRodion", "과감한 터치", "None", "None"),
            ("IntenseColorsRodion", "강렬한 색채", "None", "None"),
            ("ThankyouDocentRodion", "경의", "None", "None"),
            ("TestWaitDocentRodion", "레플렉시옹", "None", "None"),
            ("FaubismMaskMeursault", "야수파 - 마스크 드 시앵", "None", "None"),
            ("TestWaitingMeursault", "결점 보완", "None", "None"),
            ("Fauvism_Lowmorale", "떨어지는 영감", "None", "None"),
            ("Fauvism_Panic", "떨어지는 영감", "None", "None"),
            ("BoldBrushstrokes", "과감한 터치", "None", "None"),
            ("IntenseColors", "강렬한 색채", "None", "None"),
            ("FaubismWolfMask", "야수파 - 마스크 드 루", "None", "None"),
            ("FaubismWolfMaskBlooded", "야수파 - 마스크 드 루 앙상글랑테", "None", "None"),
            ("PanicChangeLock", "패닉 변경 불가", "None", "None"),
            ("IndexPrescriptEnemy_0", "지령[쪽지] I", "None", "None"),
            ("IndexPrescriptEnemy_1", "지령[쪽지] II", "None", "None"),
            ("IndexPrescriptEnemy_2", "지령[쪽지] III", "None", "None"),
            ("IndexPrescriptEnemy_3", "지령[쪽지] IV", "None", "None"),
            ("FauvismDocent_Lowmorale", "충동적인 채색", "None", "None"),
            ("FauvismDocent_Panic", "충동적인 채색", "None", "None"),
            ("RingFingerPhysical", "육체미", "None", "None"),
            ("RingFingerFauvism", "야성미", "None", "None"),
            ("ResentmentSpiderDaughterTwo", "<s>중지</s> - 원한", "None", "None"),
            ("ReinforcedTattooSpiderDaughterTwo", "<s>중지식</s> 강화문신", "None", "None"),
            ("HeatingWireOnSpiderTwo", "열선 ON", "None", "None"),
            ("BearClawWoundAlly", "찢긴 색채 [루주]", "None", "None"),
            ("EagleClawWoundAlly", "찢긴 색채 [블뢰]", "None", "None"),
        };

        // Ability 탭
        private string _abilitySearch = "";
        private int    _abilityPage   = 0;

        // (SYSTEM_ABILITY_KEYWORD 이름, 설명)
        // AddAbilityThisRound(keyword, stack, turn) 으로 주입
        // stack = 효과 수치, turn = 지속 턴
        private readonly (string id, string desc)[] _allAbilities = {
            // ── 공격/방어 ────────────────────────────────────────────────
            ("DefenseAdder",                          "방어 레벨 +stack"),
            ("ParryingResultAdder",                   "합 위력 +stack"),
            ("ParryingResultAdderIfFasterThanTarget", "속도 우위 시 합 위력 +stack"),
            ("MaxHpUpMultiplier",                     "최대 체력 배율 +stack%"),
            ("MaxHpUpAdder",                          "최대 체력 +stack"),
            // ── 속도 ─────────────────────────────────────────────────────
            ("MaxSpeedAdder",                         "속도 최댓값 +stack"),
            ("MinSpeedAdder",                         "속도 최솟값 +stack"),
            // ── 코인/정신력 ───────────────────────────────────────────────
            ("EgoResourceAdder",                      "E.G.O 자원 +stack"),
            ("MpUsageByEgoDown",                      "E.G.O 자원 소모 감소"),
            ("MpUsageByEgoUp",                        "E.G.O 자원 소모 증가"),
            ("MentalSystemResultIncreaseUp",          "정신력 회복량 증가"),
            ("MentalSystemResultIncreaseDown",        "정신력 회복량 감소"),
            ("MentalSystemResultDecreaseUp",          "정신력 손실량 증가"),
            ("MentalSystemResultDecreaseDown",        "정신력 손실량 감소"),
            // ── 코인 고정 ─────────────────────────────────────────────────
            ("ForceHeadOnAllCoinInAllSlots",          "전체 코인 앞면 고정"),
            ("ForceTailOnParrying",                   "클래시 코인 뒷면 고정"),
            ("ForceHeadOnParrying",                   "클래시 코인 앞면 고정"),
            ("ForceOpponentHeadOnParrying",           "상대 클래시 코인 앞면 고정"),
            ("ForceOpponentTailOnParrying",           "상대 클래시 코인 뒷면 고정"),
            // ── 대상 지정 ─────────────────────────────────────────────────
            ("AttackFastestEnemy",                    "가장 빠른 적 공격"),
            ("AttackSlowestEnemy",                    "가장 느린 적 공격"),
            // ── 보호막/불사 ───────────────────────────────────────────────
            ("Shield_NextTurn",                       "다음 턴 보호막 +stack"),
            ("Immortal",                              "불사 (즉사 무효)"),
            ("Immortal_If_Not_Alone",                 "혼자가 아닐 때 불사"),
            // ── 피해 관련 ─────────────────────────────────────────────────
            ("TakeBsDmgMultiplier",                   "흐트러짐 피해 배율"),
            ("AttackDmgupByStackRatio",               "스택 비율 공격 피해 증가"),
            ("SystemAbility_TakeDamageMultiplier",    "받는 피해 배율"),
            // ── 기타 ─────────────────────────────────────────────────────
            ("BlockMentalCorrision",                  "침식 차단"),
            ("SystemAbility_CantRetreat",             "후퇴 불가"),
            ("IsTargetableFalse",                     "타겟 불가"),
            ("IsActionableFalse",                     "행동 불가"),
            ("BreakOnRoundEnd",                       "턴 종료 시 흐트러짐"),
            ("ReactiveShield_VibrationExplosion",     "진동 폭발 시 보호막"),
            ("ReactiveShield_SinkingTurn",            "침잠 횟수 시 보호막"),
            // ── 특수 (흑수 시너지) ────────────────────────────────────────
            ("KCorpHongluPassive",                    "K사 홍루 패시브"),
            ("RCorpMeursaultDefense",                 "R사 뫼르소 방어"),
            ("CumulativeLacerationSystem",            "누적 출혈 시스템"),
        };

        private const int PAGE_SIZE = 12;

        // 창
        private Rect    _windowRect = new Rect(20, 20, 560, 680);
        private bool    _isDragging = false;
        private Vector2 _dragOffset = Vector2.zero;

        // ── 초기화 ────────────────────────────────────────────────────────
        public InjectorUI(IntPtr ptr) : base(ptr) { }

        private void Start() { }

        // ── 업데이트 ──────────────────────────────────────────────────────
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                _showPanel = !_showPanel;
        }

        // ── GUI ───────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!_showPanel) return;

            GUI.Box(_windowRect, "LimbusInjector ver.1.6.0");

            // 드래그
            var titleBar = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, 20);
            var e = Event.current;
            if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
            {
                _isDragging = true;
                _dragOffset = new Vector2(_windowRect.x - e.mousePosition.x,
                                          _windowRect.y - e.mousePosition.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                _windowRect.x = e.mousePosition.x + _dragOffset.x;
                _windowRect.y = e.mousePosition.y + _dragOffset.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
            }

            GUILayout.BeginArea(new Rect(
                _windowRect.x + 5,
                _windowRect.y + 22,
                _windowRect.width - 10,
                _windowRect.height - 27));

            DrawFactionToggle();
            GUILayout.Space(3);
            DrawUnitList();
            GUILayout.Space(3);
            DrawTabBar();
            GUILayout.Space(3);

            if (_activeTab == 0) DrawBuffTab();
            else                 DrawAbilityTab();

            GUILayout.Space(4);
            GUILayout.Label(string.IsNullOrEmpty(_status) ? " " : _status);

            GUILayout.EndArea();
        }

        // ── 진영 토글 ─────────────────────────────────────────────────────
        private void DrawFactionToggle()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Faction:", GUILayout.Width(55));
            if (GUILayout.Toggle(_faction == UNIT_FACTION.PLAYER, "Player", "Button", GUILayout.Width(80)))
            {
                if (_faction != UNIT_FACTION.PLAYER)
                { _faction = UNIT_FACTION.PLAYER; _selectedUnit = null; _selectedInstanceID = -1; _selectedName = ""; }
            }
            if (GUILayout.Toggle(_faction == UNIT_FACTION.ENEMY, "Enemy", "Button", GUILayout.Width(80)))
            {
                if (_faction != UNIT_FACTION.ENEMY)
                { _faction = UNIT_FACTION.ENEMY; _selectedUnit = null; _selectedInstanceID = -1; _selectedName = ""; }
            }
            GUILayout.EndHorizontal();
        }

        // ── 유닛 목록 ─────────────────────────────────────────────────────
        private void DrawUnitList()
        {
            GUILayout.Label("── Unit ──");
            var bom = FindObjectOfType<BattleObjectManager>();
            if (bom == null)
            {
                GUILayout.Label("[전투 중 아님]");
                return;
            }

            var listMethod = typeof(BattleObjectManager).GetMethod(
                "GetModelList",
                new System.Type[]{ typeof(UNIT_FACTION), typeof(bool) });
            if (listMethod == null) { GUILayout.Label("[GetModelList 없음]"); return; }

            var result  = listMethod.Invoke(bom, new object[]{ _faction, false });
            var countProp = result.GetType().GetProperty("Count");
            var indexer   = result.GetType().GetProperty("Item");
            int count = (int)countProp.GetValue(result);

            if (count == 0) { GUILayout.Label("[유닛 없음]"); return; }

            GUILayout.BeginHorizontal();
            for (int i = 0; i < count; i++)
            {
                var unit       = indexer.GetValue(result, new object[]{ i });
                var wasProp    = unit.GetType().GetProperty("WasCollected");
                if ((bool)wasProp.GetValue(unit)) continue;

                var idProp     = unit.GetType().GetProperty("InstanceID");
                int instanceID = (int)idProp.GetValue(unit);

                var nameMethod = unit.GetType().GetMethod("GetUniqueName");
                string name    = nameMethod.Invoke(unit, null) as string ?? $"ID:{instanceID}";

                bool isSel = _selectedInstanceID == instanceID;
                string label = isSel ? $"[{name}]" : name;

                if (GUILayout.Button(label, GUILayout.MaxWidth(88)))
                {
                    _selectedUnit       = unit;
                    _selectedInstanceID = instanceID;
                    _selectedName       = name;
                    _status = $"선택: {name} (InstanceID {instanceID})";
                }
            }
            GUILayout.EndHorizontal();
        }

        // ── 탭 바 ─────────────────────────────────────────────────────────
        private void DrawTabBar()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_activeTab == 0, "Buff",    "Button", GUILayout.Width(100)))
                _activeTab = 0;
            if (GUILayout.Toggle(_activeTab == 1, "Ability", "Button", GUILayout.Width(100)))
                _activeTab = 1;
            GUILayout.EndHorizontal();
        }

        // ── Buff 탭 ───────────────────────────────────────────────────────
        private void DrawBuffTab()
        {
            // Stack / Turn 입력
            GUILayout.BeginHorizontal();
            GUILayout.Label("Stack:", GUILayout.Width(40));
            _stackInput = GUILayout.TextField(_stackInput, GUILayout.Width(40));
            GUILayout.Label("Turn:", GUILayout.Width(36));
            _turnInput  = GUILayout.TextField(_turnInput,  GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // 타입 필터
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _buffTypeLabels.Length; i++)
            {
                int idx = i;
                if (GUILayout.Toggle(_buffTypeIdx == idx, _buffTypeLabels[i], "Button", GUILayout.Width(80)))
                {
                    if (_buffTypeIdx != idx) { _buffTypeIdx = idx; _buffPage = 0; }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // 검색
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(48));
            string newSearch = GUILayout.TextField(_buffSearch, GUILayout.Width(300));
            if (newSearch != _buffSearch) { _buffSearch = newSearch; _buffPage = 0; }
            if (GUILayout.Button("X", GUILayout.Width(28))) { _buffSearch = ""; _buffPage = 0; }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // 필터
            string qLower = _buffSearch.Trim().ToLowerInvariant();
            var filtered = new List<(string id, string kr, string buffType, string buffClass)>();
            foreach (var kw in _allBuffKeywords)
            {
                bool typeMatch = _buffTypeIdx == 0
                    || (_buffTypeIdx == 1 && kw.buffType == "Positive")
                    || (_buffTypeIdx == 2 && kw.buffType == "Negative")
                    || (_buffTypeIdx == 3 && (kw.buffClass == "SinBuff" || kw.buffClass == "CollapsableSinBuff"))
                    || (_buffTypeIdx == 4 && kw.buffType == "None" && kw.buffClass != "SinBuff" && kw.buffClass != "CollapsableSinBuff");
                bool searchMatch = string.IsNullOrEmpty(qLower)
                    || kw.id.ToLowerInvariant().Contains(qLower)
                    || kw.kr.ToLowerInvariant().Contains(qLower);
                if (typeMatch && searchMatch) filtered.Add(kw);
            }

            GUILayout.Label($"총 {filtered.Count}개");
            GUILayout.Space(2);

            // 페이지 목록
            int start = _buffPage * PAGE_SIZE;
            int end   = Math.Min(start + PAGE_SIZE, filtered.Count);
            for (int i = start; i < end; i++)
            {
                var kw = filtered[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(kw.kr,  GUILayout.Width(200));
                GUILayout.Label($"[{kw.id}]", GUILayout.Width(220));
                if (GUILayout.Button("+", GUILayout.Width(28))) InjectBuff(kw.id);
                GUILayout.EndHorizontal();
            }

            DrawPageNav(filtered.Count, ref _buffPage);
        }

        // ── Ability 탭 ────────────────────────────────────────────────────
        private void DrawAbilityTab()
        {
            // Stack / Turn 입력 (Ability도 stack, turn 파라미터 있음)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Stack:", GUILayout.Width(40));
            _stackInput = GUILayout.TextField(_stackInput, GUILayout.Width(40));
            GUILayout.Label("Turn:", GUILayout.Width(36));
            _turnInput  = GUILayout.TextField(_turnInput,  GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(48));
            string newSearch = GUILayout.TextField(_abilitySearch, GUILayout.Width(340));
            if (newSearch != _abilitySearch) { _abilitySearch = newSearch; _abilityPage = 0; }
            if (GUILayout.Button("X", GUILayout.Width(28))) { _abilitySearch = ""; _abilityPage = 0; }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            // 필터
            string queryLower = _abilitySearch.Trim().ToLowerInvariant();
            var filtered = new List<(string id, string desc)>();
            foreach (var entry in _allAbilities)
            {
                if (string.IsNullOrEmpty(queryLower)
                    || entry.id.ToLowerInvariant().Contains(queryLower)
                    || entry.desc.ToLowerInvariant().Contains(queryLower))
                    filtered.Add(entry);
            }

            GUILayout.Label($"총 {filtered.Count}개");
            GUILayout.Space(2);

            // 페이지 목록
            int start = _abilityPage * PAGE_SIZE;
            int end   = Math.Min(start + PAGE_SIZE, filtered.Count);
            for (int i = start; i < end; i++)
            {
                var entry = filtered[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{entry.desc}", GUILayout.Width(360));
                GUILayout.Label($"[{entry.id}]", GUILayout.Width(60));
                if (GUILayout.Button("+", GUILayout.Width(28))) InjectAbility(entry.id);
                GUILayout.EndHorizontal();
            }

            DrawPageNav(filtered.Count, ref _abilityPage);
        }

        // ── 페이지 내비게이션 ─────────────────────────────────────────────
        private void DrawPageNav(int total, ref int page)
        {
            int totalPages = Math.Max(1, (total + PAGE_SIZE - 1) / PAGE_SIZE);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(35)) && page > 0) page--;
            GUILayout.Label($"{page + 1} / {totalPages}", GUILayout.Width(65));
            if (GUILayout.Button(">", GUILayout.Width(35)) && page < totalPages - 1) page++;
            GUILayout.EndHorizontal();
        }

        // ── 주입 ─────────────────────────────────────────────────────────
        private void InjectBuff(string keywordName)
        {
            if (!CheckUnitSelected()) return;

            if (!Enum.TryParse(typeof(BUFF_UNIQUE_KEYWORD), keywordName, out var boxed))
            {
                _status = $"[오류] 알 수 없는 키워드: {keywordName}";
                return;
            }

            if (!int.TryParse(_stackInput, out int stack) || stack <= 0) stack = 1;
            if (!int.TryParse(_turnInput,  out int turn)  || turn  <= 0) turn  = 3;

            try
            {
                var buffMethod = _selectedUnit!.GetType().GetMethod("AddBuff_NonGiver");
                object addedStack = 0, addedTurn = 0, overStack = 0, overTurn = 0;
                var args = new object[]{
                    boxed,
                    stack,
                    turn,
                    0,
                    (ABILITY_SOURCE_TYPE)0,
                    (BATTLE_EVENT_TIMING)0,
                    null,
                    addedStack, addedTurn, overStack, overTurn
                };
                buffMethod.Invoke(_selectedUnit, args);
                _status = $"[Buff] {keywordName} x{stack}/{turn}t -> {_selectedName} (added:{args[7]})";
                LimbusInjectorPlugin.Log?.LogInfo(_status);
            }
            catch (Exception ex)
            {
                _status = "[오류] " + ex.Message;
                LimbusInjectorPlugin.Log?.LogError(ex.ToString());
            }
        }

        private void InjectAbility(string abilityId)
        {
            if (!CheckUnitSelected()) return;

            try
            {
                if (!Enum.TryParse(typeof(SYSTEM_ABILITY_KEYWORD), abilityId, out var boxed))
                {
                    _status = $"[오류] 알 수 없는 키워드: {abilityId}";
                    return;
                }

                if (!int.TryParse(_stackInput, out int stack) || stack <= 0) stack = 1;
                if (!int.TryParse(_turnInput,  out int turn)  || turn  <= 0) turn  = 3;

                var addMethod = _selectedUnit!.GetType().GetMethod("AddAbilityThisRound");
                addMethod.Invoke(_selectedUnit, new object[]{ boxed, stack, turn });
                _status = $"[Ability] {abilityId} x{stack}/{turn}t -> {_selectedName}";
                LimbusInjectorPlugin.Log?.LogInfo(_status);
            }
            catch (Exception ex)
            {
                _status = "[오류] " + ex.Message;
                LimbusInjectorPlugin.Log?.LogError(ex.ToString());
            }
        }

        // ── 유틸 ─────────────────────────────────────────────────────────
        private bool CheckUnitSelected()
        {
            if (_selectedUnit == null)
            {
                _status = "[오류] 유닛이 선택되지 않았습니다.";
                return false;
            }
            var wasProp = _selectedUnit.GetType().GetProperty("WasCollected");
            if (wasProp != null && (bool)wasProp.GetValue(_selectedUnit))
            {
                _selectedUnit = null;
                _selectedInstanceID = -1;
                _selectedName = "";
                _status = "[오류] 선택한 유닛이 더 이상 유효하지 않습니다.";
                return false;
            }
            return true;
        }
    }
}