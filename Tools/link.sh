#!/bin/sh
#
# link.sh --
#
# Eagle Enterprise Edition (EEE) -- meld the enterprise overlay into the parent
# Eagle core checkout using symbolic links, so that the enterprise solutions,
# plugin projects, signing keys, and shared sources appear at the core-relative
# paths their project files expect.
#
# Intended use: clone the Eagle core super-repository with submodules
# (`git clone --recurse-submodules ...`), then run this script from the `eee`
# submodule.  It links every file/directory under `<eee>/Eagle/` into the
# matching path of the parent core checkout.  The links are RELATIVE, so the
# melded clone is self-contained and relocatable.
#
# It is idempotent (re-running re-uses existing correct links), refuses to
# overwrite real core files (reports a conflict instead), and fails fast if it
# cannot create symbolic links.  Use `--unlink` to remove the links it created
# and `--dry-run` to preview.
#
# Options:
#   --dry-run        show what would happen; make no changes
#   --unlink         remove the EEE links from the core (instead of creating)
#   --core <dir>     parent Eagle core checkout (default: auto-detected)
#   -h, --help       this help
#
# Environment:
#   EEE_CORE_ROOT    same as --core (the option wins if both are given)
#
set -eu

OVERLAY_SUBDIR=Eagle
DRY_RUN=0
ACTION=link
CORE_OVERRIDE=

usage() {
    sed -n '3,33p' "$0" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --dry-run) DRY_RUN=1 ;;
        --unlink)  ACTION=unlink ;;
        --core)    shift; [ $# -gt 0 ] || { echo "error: --core needs an argument" >&2; exit 2; }; CORE_OVERRIDE=$1 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "error: unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
    shift
done

# --- resolve locations -------------------------------------------------------

# Directory of this script, following symlinks.
s=$0
while [ -h "$s" ]; do
    d=$(cd -P "$(dirname "$s")" >/dev/null 2>&1 && pwd)
    s=$(readlink "$s")
    case $s in /*) ;; *) s=$d/$s ;; esac
done
script_dir=$(cd -P "$(dirname "$s")" >/dev/null 2>&1 && pwd)

eee_root=$(cd "$script_dir/.." >/dev/null 2>&1 && pwd)   # <eee>/Tools -> <eee>
overlay="$eee_root/$OVERLAY_SUBDIR"

[ -d "$overlay" ] || { echo "error: EEE overlay not found: $overlay" >&2; exit 1; }

# Parent Eagle core checkout: explicit override, else the git superproject, else
# the parent directory of the eee submodule.
core_root=${CORE_OVERRIDE:-${EEE_CORE_ROOT:-}}
if [ -z "$core_root" ]; then
    core_root=$(git -C "$eee_root" rev-parse --show-superproject-working-tree 2>/dev/null || true)
fi
[ -n "$core_root" ] || core_root=$(cd "$eee_root/.." >/dev/null 2>&1 && pwd)
core_root=$(cd "$core_root" >/dev/null 2>&1 && pwd) || { echo "error: bad core root" >&2; exit 1; }

# Sanity: does this look like an Eagle core checkout?
if [ ! -e "$core_root/Eagle.sln" ] && [ ! -d "$core_root/Library" ]; then
    echo "error: '$core_root' does not look like an Eagle core checkout" >&2
    echo "       (no Eagle.sln or Library/).  Pass --core <dir> or set EEE_CORE_ROOT." >&2
    exit 1
fi

# Path of the eee submodule relative to the core (normally 'eee'); used to build
# the relative symlink targets.
case "$eee_root/" in
    "$core_root"/*) sub_rel=${eee_root#"$core_root"/} ;;
    *) echo "error: eee root '$eee_root' is not located under core '$core_root'" >&2; exit 1 ;;
esac

# --- fail fast: can we create symbolic links here? ---------------------------

if [ "$ACTION" = link ] && [ "$DRY_RUN" = 0 ]; then
    probe="$core_root/.eee-link-probe.$$"
    rm -f "$probe" 2>/dev/null || true
    if ! ln -s . "$probe" 2>/dev/null; then
        echo "error: cannot create symbolic links in '$core_root'." >&2
        echo "       (on Windows, run as Administrator or enable Developer Mode)." >&2
        exit 1
    fi
    rm -f "$probe"
fi

# --- helpers -----------------------------------------------------------------

created=0
skipped=0
removed=0

# Relative symlink target for a core-relative path: ('../' * depth-of-parent) +
# <sub_rel>/<OVERLAY_SUBDIR>/<rel>.
rel_target() {
    local rel up rest
    rel=$1; up=""; rest=$rel
    while [ "${rest#*/}" != "$rest" ]; do up="../$up"; rest=${rest#*/}; done
    printf '%s%s/%s/%s' "$up" "$sub_rel" "$OVERLAY_SUBDIR" "$rel"
}

is_our_link() {     # rel -> 0 if core/rel is a symlink to the expected target
    local dst
    dst="$core_root/$1"
    [ -L "$dst" ] || return 1
    [ "$(readlink "$dst")" = "$(rel_target "$1")" ]
}

do_link() {         # rel
    local rel dst tgt parent
    rel=$1; dst="$core_root/$rel"; tgt=$(rel_target "$rel"); parent=$(dirname "$dst")
    if [ "$DRY_RUN" = 1 ]; then
        [ -d "$parent" ] || echo "  mkdir   $(dirname "$rel")"
        echo "  link    $rel -> $tgt"
    else
        [ -d "$parent" ] || mkdir -p "$parent"
        ln -s "$tgt" "$dst"
        echo "  linked  $rel -> $tgt"
    fi
    created=$((created + 1))
}

do_unlink() {       # rel
    local rel dst
    rel=$1; dst="$core_root/$rel"
    if [ "$DRY_RUN" = 1 ]; then echo "  unlink  $rel"; else rm -f "$dst"; echo "  removed $rel"; fi
    removed=$((removed + 1))
}

# --- recursive overlay walk --------------------------------------------------

walk() {            # base ('' for the overlay root)
    local base src name rel dst
    base=$1
    for src in "$overlay${base:+/$base}"/*; do
        [ -e "$src" ] || [ -L "$src" ] || continue   # empty-glob guard
        name=${src##*/}
        rel="${base:+$base/}$name"
        dst="$core_root/$rel"

        if [ -L "$dst" ]; then
            # Existing link: must be ours, in either mode.
            if is_our_link "$rel"; then
                if [ "$ACTION" = unlink ]; then do_unlink "$rel"; else echo "  ok      $rel"; skipped=$((skipped + 1)); fi
            else
                echo "CONFLICT: '$rel' is a symlink to an unexpected target: $(readlink "$dst")" >&2
                exit 1
            fi
        elif [ -d "$src" ] && [ ! -L "$src" ] && [ -d "$dst" ]; then
            # Directory present in BOTH overlay and core: descend (shared dir).
            walk "$rel"
        elif [ "$ACTION" = unlink ]; then
            : # nothing of ours here to remove
        elif [ -e "$dst" ]; then
            echo "CONFLICT: '$rel' already exists in the core and is not an EEE link." >&2
            exit 1
        else
            do_link "$rel"   # absent in core: link the file, or the whole dir
        fi
    done
}

# --- run ---------------------------------------------------------------------

echo "EEE overlay : $overlay"
echo "Eagle core  : $core_root"
echo "Submodule   : $sub_rel"
echo "Action      : $ACTION$([ "$DRY_RUN" = 1 ] && echo ' (dry-run)')"
echo

walk ""

echo
if [ "$ACTION" = unlink ]; then
    echo "Done. $removed link(s) removed$([ "$DRY_RUN" = 1 ] && echo ' (dry-run)')."
else
    echo "Done. $created link(s) created, $skipped already in place$([ "$DRY_RUN" = 1 ] && echo ' (dry-run)')."
fi
