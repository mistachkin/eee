#!/bin/sh
#
# link.sh --
#
# Eagle Enterprise Edition (EEE) -- meld the enterprise overlay and the parent
# Eagle core checkout together with symbolic links, so the whole thing builds
# from one tree.  Linking happens in BOTH directions:
#
#   overlay -> core : every file/directory under `<eee>/Eagle/` is linked into
#     the matching path of the parent core checkout, so the enterprise
#     solutions, plugin projects, and signing keys appear at the core-relative
#     paths their project files expect.
#
#   core -> overlay : the plugin `.csproj` files use EagleDir = `<eee>/Eagle`
#     and reference the core Library project and Targets as `<eee>/Eagle/Library`
#     and `<eee>/Eagle/Targets`, so those names are linked back to the core's own
#     `Library/` and `Targets/` directories (see CORE_DIR_LINKS below).
#
# Intended use: clone the Eagle core super-repository with submodules
# (`git clone --recurse-submodules ...`), then run this script from the `eee`
# submodule.  All links are RELATIVE, so the melded clone is self-contained and
# relocatable.
#
# It is idempotent (re-running re-uses existing correct links), refuses to
# overwrite real files (reports a conflict instead), and fails fast if it cannot
# create symbolic links.  Use `--unlink` to remove the links it created and
# `--dry-run` to preview.
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

# Directory names that live under `<eee>/Eagle` as links BACK to the core
# checkout (the "core -> overlay" direction described above).  The core must
# actually provide each one; a name the core lacks is skipped.
CORE_DIR_LINKS="Library Targets"

DRY_RUN=0
ACTION=link
CORE_OVERRIDE=

usage() {
    # print this script's leading comment block (minus the shebang), unprefixed
    sed -n '2,/^[^#]/p' "$0" | sed '$d; s/^# \{0,1\}//'
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

# --- core -> overlay links ---------------------------------------------------

# Relative symlink target from `<eee>/<OVERLAY_SUBDIR>` up to `<core>/<name>`:
# one '../' per path component in `<sub_rel>/<OVERLAY_SUBDIR>`, then <name>.
core_uplink_target() {  # name
    local name rest up
    name=$1; rest="$sub_rel/$OVERLAY_SUBDIR"; up=""
    while [ -n "$rest" ]; do
        up="../$up"
        case "$rest" in */*) rest=${rest%/*} ;; *) rest="" ;; esac
    done
    printf '%s%s' "$up" "$name"
}

# Create (or, with --unlink, remove) one core-dir link: `<eee>/Eagle/<name>`
# -> `<core>/<name>`.  Lives in the overlay, points back at the core.
do_core_link() {    # name
    local name src tgt cur
    name=$1; src="$overlay/$name"; tgt=$(core_uplink_target "$name")

    if [ -L "$src" ]; then
        cur=$(readlink "$src")
        if [ "$cur" != "$tgt" ]; then
            echo "CONFLICT: '$OVERLAY_SUBDIR/$name' is a symlink to an unexpected target: $cur" >&2
            exit 1
        fi
        if [ "$ACTION" = unlink ]; then
            if [ "$DRY_RUN" = 1 ]; then echo "  unlink  $OVERLAY_SUBDIR/$name"; else rm -f "$src"; echo "  removed $OVERLAY_SUBDIR/$name"; fi
            removed=$((removed + 1))
        else
            echo "  ok      $OVERLAY_SUBDIR/$name -> $tgt"; skipped=$((skipped + 1))
        fi
        return
    fi

    [ "$ACTION" = unlink ] && return   # nothing of ours to remove

    if [ -e "$src" ]; then
        echo "CONFLICT: '$OVERLAY_SUBDIR/$name' already exists in the overlay and is not an EEE link." >&2
        exit 1
    fi
    if [ ! -d "$core_root/$name" ]; then
        echo "  skip    $OVERLAY_SUBDIR/$name (core has no '$name/')"
        return
    fi
    if [ "$DRY_RUN" = 1 ]; then
        echo "  link    $OVERLAY_SUBDIR/$name -> $tgt"
    else
        ln -s "$tgt" "$src"; echo "  linked  $OVERLAY_SUBDIR/$name -> $tgt"
    fi
    created=$((created + 1))
}

# --- recursive overlay walk --------------------------------------------------

walk() {            # base ('' for the overlay root)
    local base src name rel dst
    base=$1
    for src in "$overlay${base:+/$base}"/*; do
        [ -e "$src" ] || [ -L "$src" ] || continue   # empty-glob guard
        name=${src##*/}
        # at the overlay root, the core-dir links point the other way (core ->
        # overlay) and are handled by do_core_link, so skip them here.
        if [ -z "$base" ]; then
            case " $CORE_DIR_LINKS " in *" $name "*) continue ;; esac
        fi
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

# core -> overlay links first when creating (so the tree is complete), last when
# removing; the overlay -> core walk skips these names either way.
if [ "$ACTION" = link ]; then
    for name in $CORE_DIR_LINKS; do do_core_link "$name"; done
    walk ""
else
    walk ""
    for name in $CORE_DIR_LINKS; do do_core_link "$name"; done
fi

echo
if [ "$ACTION" = unlink ]; then
    echo "Done. $removed link(s) removed$([ "$DRY_RUN" = 1 ] && echo ' (dry-run)')."
else
    echo "Done. $created link(s) created, $skipped already in place$([ "$DRY_RUN" = 1 ] && echo ' (dry-run)')."
fi
