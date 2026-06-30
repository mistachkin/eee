#! /bin/sh
# -*- tcl -*- \
exec tclsh "$0" ${1+"$@"}

###############################################################################
#
# renew.cgi --
#
# Extensible Adaptable Generalized Logic Engine (Eagle)
# Enterprise Edition Certificate Renewal Proxy Server
#
# Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
#
# See the file "license.terms" for information on usage and redistribution of
# this file, and for a DISCLAIMER OF ALL WARRANTIES.
#
# RCS: @(#) $Id: $
#
###############################################################################

package require Tcl 8.4
package require http 2.0

namespace eval Harpy {
  #
  # NOTE: Check if the certificate renewal URI has been set in the process
  #       environment.  If so, use it; otherwise, use the default.
  #
  if {[info exists env(RENEW_URI)]} then {
    variable uri $env(RENEW_URI)
  } else {
    variable uri https://mistachkin.nyc:11452/kapok/certificate/renew.cgi
  }

  #
  # NOTE: Setup the other default parameters for the script.
  #
  variable script [file normalize [info script]]
  variable path [file dirname $script]
  variable application [file rootname [file tail $script]]
  variable logFileName ""
  variable logLevel -1; # NOTE: Higher means more output (e.g. 0 to 5).

  #
  # NOTE: This procedure returns all its arguments joined together into a
  #       single string, joining each argument with a single space.
  #
  proc appendArgs { args } {
    eval append result $args
  }

  #
  # NOTE: This procedure writes a diagnostic message to the log file if the
  #       message level is is less than or equal to the configured logging
  #       level.  Returns non-zero if the message was actually logged.
  #
  proc writeLog { string {level 0} } {
    variable application
    variable logFileName
    variable logLevel
    variable path

    if {$level <= $logLevel} then {
      if {[string length $logFileName] == 0} then {
        set logFileName [file join $path [appendArgs \
            $application - [pid] - [clock seconds] .log]]
      }

      set channelId [open $logFileName {WRONLY CREAT APPEND}]
      fconfigure $channelId -encoding binary -translation auto
      puts -nonewline $channelId $string
      close $channelId

      return true
    }

    return false
  }

  #
  # NOTE: This procedure strips the specified value of all CR and LF
  #       characters, replacing each one with a single space character.
  #       Surrounding white-space is also removed prior to the value being
  #       returned.
  #
  proc formatValue { text } {
    return [string map [list \r " " \n " "] [string trim $text]]
  }

  #
  # NOTE: This procedure returns a properly formatted HTTP response, based
  #       on the arguments specified by the caller.  The default HTTP status
  #       code is 200 (i.e. success), the default reason is "OK", and the
  #       default content type is "text/plain".
  #
  proc formatResponse { code reason {text ""} {contentType ""} {headers ""} } {
    #
    # NOTE: Check if the HTTP status code is valid; otherwise, just use the
    #       default.
    #
    if {[string length $code] == 0 || \
        ![string is integer -strict $code]} then {
      set code 200
    }

    #
    # NOTE: Check if the HTTP status reason is valid; otherwise, just use the
    #       default.
    #
    if {[string length $reason] == 0} then {
      set reason OK
    }

    #
    # NOTE: If there is content to send, use it; otherwise, send the HTTP
    #       status code and reason as the content.
    #
    set status [appendArgs $code " " [formatValue $reason] \r\n]

    if {[string length $text] == 0} then {
      set text $status
    }

    #
    # NOTE: Check if the HTTP content type is valid; otherwise, just use the
    #       default.
    #
    if {[string length $contentType] == 0} then {
      set contentType text/plain
    }

    #
    # NOTE: Append a properly formatted HTTP status line to the result.
    #
    append result "Status: " $status

    #
    # NOTE: Check for the list of optional headers.  If present, join them
    #       with CR/LF pairs and append them to the result followed by a
    #       single CR/LF pair.
    #
    if {[llength $headers] > 0} then {
      append result [join $headers \r\n] \r\n
    }

    #
    # NOTE: Finally, append the HTTP content type/length to the result, along
    #       with the actual content, if any.  Since the HTTP content length is
    #       always the last header, make sure it ends with two CR/LF pairs.
    #
    append result "Content-Type: " [formatValue $contentType] \r\n
    append result "Content-Length: " [string bytelength $text] \r\n\r\n
    append result $text
  }

  #
  # NOTE: This procedure writes the HTTP response to the client, optionally
  #       logging it as well.
  #
  proc writeResponse { text } {
    catch {writeLog [appendArgs "response: " $text \n] 3}
    puts -nonewline stdout $text
  }

  #
  # NOTE: This procedure returns the remote host name.  If the remote host
  #       name is not available, the remote address is returned instead.  If
  #       neither are available, "unknown" is returned.
  #
  proc getRemoteHostOrAddr {} {
    global env

    if {[info exists env(REMOTE_HOST)] && \
        [string length $env(REMOTE_HOST)] > 0} then {
      return $env(REMOTE_HOST)
    } elseif {[info exists env(REMOTE_ADDR)] && \
        [string length $env(REMOTE_ADDR)] > 0} then {
      return $env(REMOTE_ADDR)
    } else {
      return unknown
    }
  }

  #
  # NOTE: This procedure is designed to issue a single HTTP request, then
  #       synchronously wait for a response, and then return the response
  #       code, reason, and text.
  #
  proc getUri { uri args } {
    if {[info exists ::tcl_version] && $::tcl_version >= 8.6} then {
      namespace eval ::tcl::unsupported {}
      set ::tcl::unsupported::socketAF inet
    }

    if {[catch {package require tls}] == 0} then {
      ::http::register https 443 [list ::tls::socket -tls1 true]

      if {[string tolower [string range $uri 0 6]] eq "http://"} then {
        set uri [appendArgs https:// [string range $uri 7 end]]
      }
    }

    set token [eval ::http::getUri [list $uri] $args]
    set http [::http::code $token]; set data [::http::data $token]
    ::http::cleanup $token

    set code 500; set reason "Internal Server Error"
    regexp -line { ([0-9]{3}) (.*)$} $http dummy code reason
    return [list $code $reason $data]
  }

  #
  # NOTE: This procedure acts as the entry point for this CGI script.  Its
  #       entire contents will be evaluated in the parent scope, due to use
  #       of the [uplevel] command within it.
  #
  proc main {} {
    #
    # NOTE: Evaluate in the parent (i.e. [namespace eval]) scope.
    #
    uplevel 1 {
      if {![info exists env(REQUEST_METHOD)] || \
          $env(REQUEST_METHOD) ne "GET"} then {
        #
        # NOTE: For now, this CGI script only supports the GET method.
        #       Issue an error response message to the client.
        #
        writeResponse [formatResponse \
            405 "Method Not Allowed" "" "" [list "Allow: GET"]]; return
      }

      if {![info exists env(QUERY_STRING)] || \
          [string length $env(QUERY_STRING)] == 0} then {
        #
        # NOTE: There must be a query string in order to forward the
        #       request.  Issue an error response message to the client.
        #
        writeResponse [formatResponse 400 "Bad Request"]; return
      }

      #
      # NOTE: Attempt to forward the query string to the configured remote
      #       URI.  If this fails, the script error will be caught and a
      #       generic error response message will be issued; otherwise, the
      #       client will receive precisely what we receive from the remote
      #       URI (i.e. at least in terms of HTTP status code, reason, and
      #       content).
      #
      writeLog [appendArgs "request: " [getRemoteHostOrAddr] ? \
          $env(QUERY_STRING) \n] 2

      if {[catch {getUri ${uri}?${env(QUERY_STRING)}} result] == 0} then {
        #
        # NOTE: This is probably success.  Return the resulting HTTP status
        #       code, reason, and content to the client.
        #
        writeResponse [eval formatResponse $result]
      } else {
        #
        # NOTE: Failure.  Return an HTTP error message to the client.
        #
        writeLog [appendArgs "failed HTTP GET: " $result \n] 1
        writeResponse [formatResponse 503 "Service Unavailable"]
      }
    }
  }

  if {1} then {
    #
    # NOTE: This is the entry point for the CGI script.  For now, it calls
    #       the entry point procedure, which is named "main" by convention.
    #
    if {[catch {main} result] != 0} then {
      writeLog [appendArgs "script error: " $result \n] 0
    }
  }
}
