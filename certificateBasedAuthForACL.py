from azure.confidentialledger.identity_service import ConfidentialLedgerIdentityServiceClient
import requests

ledger_name = "REPLACE_ME"
ledger_url = "https://" + ledger_name + ".confidential-ledger.azure.com"
identity_url = "https://identity.confidential-ledger.core.azure.com"


identity_client = ConfidentialLedgerIdentityServiceClient(identity_url)
network_identity = identity_client.get_ledger_identity(
     ledger_id=ledger_name
)

ledger_tls_cert_file_name = "networkcert.pem"
with open(ledger_tls_cert_file_name, "w") as cert_file:
    cert_file.write(network_identity.ledger_tls_certificate)
    
    
session = requests.Session()

response = session.post(
                "https://REPLACE_ME.confidential-ledger.azure.com/app/transactions?api-version=0.1-preview",
                verify="networkcert.pem",
     
#openssl ecparam -out "privkey_name.pem" -name "secp384r1" -genkey # command to produce private key
#openssl req -new -key "privkey_name.pem" -x509 -nodes -days 365 -out "cert.pem" -"sha384" -subj=/CN="ACL Client Cert" # command to produce the corresponding Public key
# provide private key and public key from above commands;
                cert=(
                    "C:/REPLACE_ME/cert.pem",
                    "C:/REPLACE_ME/privkey_name.pem",
                ),
                json={"contents": "hello"},
            )

print(f"result is {response.text}")
