          IMAGE="ghcr.io/${{ github.repository }}"

          # Always add MinVer version and :current
          TAGS="$IMAGE:$MINVERVERSIONOVERRIDE
          $IMAGE:current"

          IS_RELEASE="false"
          if [[ "${MINVERVERSIONOVERRIDE}" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
            IS_RELEASE="true"
          fi

          if [[ "$IS_RELEASE" == "true" ]]; then
            TAGS="$TAGS
            $IMAGE:latest"
            echo "Adding :latest (release build)"
          else
            echo "Not a release build; skipping :latest"
          fi

          # Expose as a multi-line output for build-push-action
          {
            echo "list<<EOF"
            echo "${TAGS}"
            echo "EOF"
          } >> "$GITHUB_OUTPUT"