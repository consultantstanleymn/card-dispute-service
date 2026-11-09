import sys


try:
    from cfnlint.decode import decode
except ModuleNotFoundError:
    from cfn_lint.decode import decode


def as_bool(value):
    if isinstance(value, bool):
        return value
    return str(value).lower() == "true"


def find_resources(resources, resource_type):
    return {
        name: resource
        for name, resource in resources.items()
        if isinstance(resource, dict) and resource.get("Type") == resource_type
    }


def main():
    if len(sys.argv) != 2:
        print("usage: check_template.py TEMPLATE")
        return 1

    path = sys.argv[1]

    try:
        decoded = decode(path)
    except Exception as exc:
        print(f"FAIL: cannot decode template: {exc}")
        return 1

    if isinstance(decoded, tuple):
        template, matches = decoded
    else:
        template, matches = decoded, []

    if not isinstance(template, dict):
        print("FAIL: decoded template is not an object")
        return 1

    if matches:
        print(f"FAIL: template decode produced {len(matches)} problem(s)")
        return 1

    resources = template.get("Resources", {})
    if not resources:
        print("FAIL: template has no Resources section")
        return 1

    failed = []

    def check(name, condition):
        if condition:
            print(f"PASS: {name}")
        else:
            print(f"FAIL: {name}")
            failed.append(name)

    buckets = find_resources(resources, "AWS::S3::Bucket")
    check(
        "S3 bucket encryption is enabled",
        bool(buckets)
        and all(
            bucket.get("Properties", {}).get("BucketEncryption")
            for bucket in buckets.values()
        ),
    )

    public_access_fields = (
        "BlockPublicAcls",
        "BlockPublicPolicy",
        "IgnorePublicAcls",
        "RestrictPublicBuckets",
    )
    public_access_ok = bool(buckets)
    for bucket in buckets.values():
        block = bucket.get("Properties", {}).get("PublicAccessBlockConfiguration", {})
        if not all(as_bool(block.get(field)) for field in public_access_fields):
            public_access_ok = False
    check("S3 public access block is fully enabled", public_access_ok)

    databases = find_resources(resources, "AWS::RDS::DBInstance")
    check(
        "RDS storage encryption is enabled",
        bool(databases)
        and all(
            as_bool(db.get("Properties", {}).get("StorageEncrypted"))
            for db in databases.values()
        ),
    )
    check(
        "RDS deletion protection is enabled",
        bool(databases)
        and all(
            as_bool(db.get("Properties", {}).get("DeletionProtection"))
            for db in databases.values()
        ),
    )

    alarms = find_resources(resources, "AWS::CloudWatch::Alarm")
    check("at least 3 CloudWatch alarms are present", len(alarms) >= 3)

    task_role = resources.get("TaskRole")
    wildcard_ok = isinstance(task_role, dict)
    if wildcard_ok:
        policies = task_role.get("Properties", {}).get("Policies", [])
        for policy in policies:
            document = policy.get("PolicyDocument", {})
            statements = document.get("Statement", [])
            if isinstance(statements, dict):
                statements = [statements]
            for statement in statements:
                action = statement.get("Action")
                if action == "*" or (isinstance(action, list) and "*" in action):
                    wildcard_ok = False
                    break
            if not wildcard_ok:
                break
    check("task role actions do not use wildcard", wildcard_ok)

    if failed:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
