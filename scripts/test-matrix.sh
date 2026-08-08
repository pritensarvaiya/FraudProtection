#!/bin/bash
# End-to-end verification matrix for POST /api/fraud/analyze.
# Covers all 4 input types (Message, Email, Url, Screenshot) with one
# fraud (positive) and one safe (negative) example each, so the full
# Semantic Kernel -> Gemini -> risk-mapping pipeline can be checked in one run.
#
# Usage: BASE_URL=http://localhost:5199 ./scripts/test-matrix.sh

BASE="${BASE_URL:-http://localhost:5199}/api/fraud/analyze"

run_case() {
  local label="$1"
  local inputType="$2"
  local content="$3"
  local secondary="$4"

  echo "=========================================="
  echo "CASE: $label"
  echo "=========================================="

  if [ -n "$secondary" ]; then
    body=$(printf '{"inputType":"%s","content":%s,"secondaryContent":%s}' "$inputType" "$content" "$secondary")
  else
    body=$(printf '{"inputType":"%s","content":%s}' "$inputType" "$content")
  fi

  curl -s -X POST "$BASE" -H "Content-Type: application/json" -d "$body" | python3 -m json.tool 2>/dev/null || \
  curl -s -X POST "$BASE" -H "Content-Type: application/json" -d "$body"
  echo
  echo
}

# 1. Message - FRAUD (positive)
run_case "1. Message - FRAUD" "Message" '"Dear customer, your SIM card will be blocked in 2 hours due to KYC non-compliance. To avoid this, click http://kyc-verify-now.tk/update and enter your Aadhaar number and OTP immediately."'

# 2. Message - SAFE (negative)
run_case "2. Message - SAFE" "Message" '"Hey, are we still on for the team meeting at 3pm today? Let me know if the time changed."'

# 3. Email - FRAUD (positive)
run_case "3. Email - FRAUD" "Email" '"Subject: Your Amazon order could not be delivered. Dear Customer, your package could not be delivered due to an unpaid customs fee of Rs 50. Pay now at http://amaz0n-redelivery.com/pay to avoid your order being returned. Failure to pay within 24 hours will result in permanent cancellation."' '"delivery@amaz0n-support.com"'

# 4. Email - SAFE (negative)
run_case "4. Email - SAFE" "Email" '"Subject: Your invoice #4521 from Acme Consulting. Hi, please find attached the invoice for last month'"'"'s consulting work. Let us know if you have any questions. Thanks, Acme Billing Team."' '"billing@acmeconsulting.com"'

# 5. Url - FRAUD (positive)
run_case "5. Url - FRAUD" "Url" '"http://paypal-secure-login-verify-account.ml/signin.php"'

# 6. Url - SAFE (negative)
run_case "6. Url - SAFE" "Url" '"https://www.wikipedia.org/wiki/Artificial_intelligence"'

# 7. Screenshot - FRAUD (positive)
run_case "7. Screenshot - FRAUD" "Screenshot" '"WhatsApp message screenshot text: Congratulations!! Your number has won Rs 25,00,000 in the KBC Lucky Draw 2026. To claim your prize, contact Mr. Sharma on WhatsApp +91-9xxxxxxxxx and pay a refundable registration fee of Rs 5000."'

# 8. Screenshot - SAFE (negative)
run_case "8. Screenshot - SAFE" "Screenshot" '"Instagram DM screenshot text: Hey! loved your photos from the trip, which app did you use to edit them?"'

# ---------------------------------------------------------------------------
# High-risk bank/KYC phishing, including non-English variants. These check that
# the pipeline flags urgency + OTP requests + lookalike .tk domains regardless
# of the language the lure is written in.
# ---------------------------------------------------------------------------

# 9. Message - HIGH RISK, English SBI KYC + OTP request
run_case "9. Message - HIGH RISK (SBI KYC, English)" "Message" '"URGENT: Your SBI account has been suspended due to incomplete KYC. Verify within 24 hours at http://sbi-kyc-verify.tk/login or your account will be permanently blocked. Share your OTP to confirm identity."'

# 10. Message - HIGH RISK, Hindi SBI KYC
run_case "10. Message - HIGH RISK (SBI KYC, Hindi)" "Message" '"प्रिय ग्राहक, आपका SBI खाता 24 घंटे में बंद कर दिया जाएगा। तुरंत KYC अपडेट करें: http://sbi-kyc-update.tk/verify अन्यथा आपका खाता ब्लॉक हो जाएगा।"'

# 11. Message - HIGH RISK, Gujarati bank KYC + OTP request
run_case "11. Message - HIGH RISK (Bank KYC, Gujarati)" "Message" '"પ્રિય ગ્રાહક, તમારું બેંક ખાતું બંધ થઈ જશે. તાત્કાલિક KYC અપડેટ કરો: http://bank-verify.tk/kyc અને તમારો OTP શેર કરો."'

# ---------------------------------------------------------------------------
# Low-risk controls: benign personal message, and a legitimate well-known
# domain. These guard against false positives - a link alone must not be
# enough to raise the risk score.
# ---------------------------------------------------------------------------

# 12. Message - LOW RISK, benign family message
run_case "12. Message - LOW RISK (family plans)" "Message" '"Hi Mum, dinner is at 7pm tonight. I will bring the dessert. See you soon!"'

# 13. Url - LOW RISK, legitimate well-known domain
run_case "13. Url - LOW RISK (legitimate domain)" "Url" '"https://github.com/"'
