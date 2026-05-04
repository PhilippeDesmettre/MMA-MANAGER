const ISO3_TO_ISO2 = {
  ALG: 'dz', ARG: 'ar', ARM: 'am', AUS: 'au', AZE: 'az',
  BEL: 'be', BRA: 'br', CAN: 'ca', CHN: 'cn', CMR: 'cm',
  COD: 'cd', COL: 'co', CUB: 'cu', DEU: 'de', ECU: 'ec',
  EGY: 'eg', ESP: 'es', FRA: 'fr', GBR: 'gb', GEO: 'ge',
  IRL: 'ie', IRN: 'ir', ITA: 'it', JPN: 'jp', KAZ: 'kz',
  KOR: 'kr', MAR: 'ma', MEX: 'mx', MNG: 'mn', NGA: 'ng',
  NLD: 'nl', PER: 'pe', PHL: 'ph', POL: 'pl', RUS: 'ru',
  SEN: 'sn', SWE: 'se', THA: 'th', TUN: 'tn', TUR: 'tr',
  UKR: 'ua', USA: 'us', UZB: 'uz', ZAF: 'za'
}

export function getFlagClass(iso3Code) {
  const iso2 = ISO3_TO_ISO2[iso3Code?.toUpperCase()]
  return iso2 ? `fi fi-${iso2}` : ''
}

export function getIso2(iso3Code) {
  return ISO3_TO_ISO2[iso3Code?.toUpperCase()] ?? ''
}
