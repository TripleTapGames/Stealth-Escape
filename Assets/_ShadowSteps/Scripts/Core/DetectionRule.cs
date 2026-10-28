namespace ShadowSteps.Core
{
    /// <summary>
    /// When a guard's cone captures the player. Stored per level (schema 2 <c>rules.detection</c>) so the V1 reference
    /// fixtures keep their original meaning while new content uses the current design.
    /// </summary>
    public enum DetectionRule
    {
        /// <summary>V1 reference rule: after the guards move, any cone that reaches the player's node captures.</summary>
        AfterGuardMove,
        /// <summary>
        /// V2 rule: the player's landing node must be inside guard vision both before the guards move (their poses when
        /// the player acted) and after they move. Being lit by only one of the two is safe. EMP freezes the guards, so
        /// both looks are the same pose and a single sighting captures.
        /// </summary>
        BeforeAndAfterGuardMove
    }

    public static class DetectionRuleUtil
    {
        public const string AfterToken = "afterGuardMove";
        public const string BeforeAndAfterToken = "beforeAndAfterGuardMove";

        public static string ToToken(DetectionRule rule) => rule == DetectionRule.AfterGuardMove ? AfterToken : BeforeAndAfterToken;

        public static bool TryParse(string token, out DetectionRule rule)
        {
            switch (token)
            {
                case AfterToken: rule = DetectionRule.AfterGuardMove; return true;
                case BeforeAndAfterToken: rule = DetectionRule.BeforeAndAfterGuardMove; return true;
                default: rule = DetectionRule.BeforeAndAfterGuardMove; return false;
            }
        }
    }
}
