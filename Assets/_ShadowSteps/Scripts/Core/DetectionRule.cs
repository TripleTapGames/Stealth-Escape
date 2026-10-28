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
        /// V2 rule: the player's landing node is captured if it is inside guard vision either before the guards move
        /// (their poses when the player acted) or after they move. Stricter than V1: crossing in front of a guard is
        /// lethal even if the guard turns away on the same turn. EMP freezes the guards, so both looks are the same pose.
        /// </summary>
        BeforeOrAfterGuardMove
    }

    public static class DetectionRuleUtil
    {
        public const string AfterToken = "afterGuardMove";
        public const string BeforeOrAfterToken = "beforeOrAfterGuardMove";

        public static string ToToken(DetectionRule rule) => rule == DetectionRule.AfterGuardMove ? AfterToken : BeforeOrAfterToken;

        public static bool TryParse(string token, out DetectionRule rule)
        {
            switch (token)
            {
                case AfterToken: rule = DetectionRule.AfterGuardMove; return true;
                case BeforeOrAfterToken: rule = DetectionRule.BeforeOrAfterGuardMove; return true;
                default: rule = DetectionRule.BeforeOrAfterGuardMove; return false;
            }
        }
    }
}
