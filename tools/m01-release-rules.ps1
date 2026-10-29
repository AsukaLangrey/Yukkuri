# A known official release crashed after about 47 minutes. A ten-minute run
# no longer qualifies a new M01 release, even when it completed its scenario.
function Confirm-M01NaturalPlay($Result,$Metadata) {
    if($Result.wallSeconds -lt 7200 -or $Metadata.requested_soak_seconds -lt 7200) {
        throw 'New M01 releases require at least 7200 actual seconds of natural graphical play after the long-run crash.'
    }
    if(!$Result.wildProtected -or !$Result.invited -or !$Result.naturalDefense) {
        throw 'Natural-play encounter and rescue loop is incomplete.'
    }
}
