# shellcheck shell=bash
# First-boot setup, appended to the user data by steps/compute.sh after /etc/trackmgmt/app.conf and
# /opt/trackmgmt/run-app.sh have been written. Runs as root on Amazon Linux 2023
# (which already includes the AWS CLI and the SSM agent).

# shellcheck source=/dev/null
source /etc/trackmgmt/app.conf

dnf install -y docker
systemctl enable --now docker

# Amazon's CA bundle for this region, so the API can verify the RDS certificate (SSL Mode=VerifyFull).
curl -fsSL "https://truststore.pki.rds.amazonaws.com/${AWS_REGION}/${AWS_REGION}-bundle.pem" \
  -o /etc/trackmgmt/rds-ca.pem
chmod 644 /etc/trackmgmt/rds-ca.pem

touch /etc/trackmgmt/bootstrap-done

# Start the current release, if one was deployed already (new instances from scaling or a refresh).
/opt/trackmgmt/run-app.sh || echo "Track Management: first start failed; run deploy.sh again." >&2
