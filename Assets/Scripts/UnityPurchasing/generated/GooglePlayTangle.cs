// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("OCPfpkh6aXYV8fIFHo1w43FVrOrgOkTsG0INFgU/QtrMEHrolu2Kq/KPGlabYI1kqpWlvw9cFgnlKalAMrG/sIAysbqyMrGxsDxuV74kkwX4WPe3UZyvw0M+tBfTQvamBL9mNtpUcPbmtoWbxyRxNBuLooKVIb7Aa9QmpRfFHxV0JiRkSDn3Xj8bihw7R08fDA4dcdI40xW1SuNAZcDbQKJ30/SG49i4VxU4nVwrOIJXwlm6Jph6zuPLBYu3OuNS9j8633k1AFPSKys13tz/E50MrprDN1KPd3TcmZJF0qqbrFGh0o+vX8Zrv1S0GXiCgDKxkoC9trmaNvg2R72xsbG1sLPZrde9tZG4WGH9Q4unpnFpv19ob2d4vEPaL0LcRbKzsbCx");
        private static int[] order = new int[] { 7,3,12,3,8,13,13,9,11,13,10,12,13,13,14 };
        private static int key = 176;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
