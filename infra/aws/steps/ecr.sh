# shellcheck shell=bash
# Two private ECR repositories (<prefix>-api, <prefix>-web): immutable tags, scan on push,
# encryption at rest, and a lifecycle rule that keeps the newest ECR_KEEP_IMAGES images.
# Cost: storage per GB-month (small: two images per release).

step_ecr() {
  local repo name uri policy="$WORK_DIR/ecr-lifecycle.json"
  log "Container registries (ECR)"
  printf '{"rules":[{"rulePriority":1,"description":"Keep the newest %s images","selection":{"tagStatus":"any","countType":"imageCountMoreThan","countNumber":%s},"action":{"type":"expire"}}]}' \
    "$ECR_KEEP_IMAGES" "$ECR_KEEP_IMAGES" >"$policy"

  for repo in api web; do
    name="${NAME_PREFIX}-${repo}"
    uri="$(aws_lookup 'RepositoryNotFoundException' ecr describe-repositories \
      --repository-names "$name" --query 'repositories[0].repositoryUri')"
    if [[ -z "$uri" ]]; then
      set_tags "$name"
      uri="$(awst ecr create-repository --repository-name "$name" \
        --image-tag-mutability IMMUTABLE \
        --image-scanning-configuration scanOnPush=true \
        --encryption-configuration encryptionType=AES256 \
        --tags "${TAGS[@]}" --query repository.repositoryUri)"
      log "  created $uri"
    else
      log "  exists  $uri"
    fi
    awsr ecr put-lifecycle-policy --repository-name "$name" \
      --lifecycle-policy-text "$(file_arg "$policy")" >/dev/null
  done
}
