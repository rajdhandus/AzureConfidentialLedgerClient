from azure.confidentialledger.identity_service import ConfidentialLedgerIdentityServiceClient
from azure.confidentialledger import ConfidentialLedgerClient, ConfidentialLedgerCertificateCredential

# installation pre-requisites
# https://www.python.org/downloads/
# https://pip.pypa.io/en/stable/installation/
# pip install azure.confidentialledger
# pip install azure.mgmt.confidentialledger
# pip install azure.confidentialledger

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
    
# openssl ecparam -out "privkey_name.pem" -name "secp384r1" -genkey # command to produce private key
# openssl req -new -key "privkey_name.pem" -x509 -nodes -days 365 -out "cert.pem" -"sha384" -subj=/CN="ACL Client Cert" # command to produce the corresponding Public key
# cat cert.pem privkey_name.pem > clientcert.pem

# run the commands above and produce a concatenated file containing public and private key so it can be used for creating ConfidentialLedgerCertificateCredential
credential = ConfidentialLedgerCertificateCredential("C:/REPLACE_ME/clientcert.pem")

ledger_client = ConfidentialLedgerClient(
     endpoint=ledger_url, 
     credential=credential, #use ConfidentialLedgerCertificateCredential here
     ledger_certificate_path=ledger_tls_cert_file_name
)
append_result = ledger_client.append_to_ledger(
    entry_contents="Hello world, again!", wait_for_commit=True
)

print(f"result is {append_result}")