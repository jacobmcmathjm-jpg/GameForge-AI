const GameForgeLicenceCommercialTracker = {
  records: [],
  add(record) { this.records.push(record); },
  report() { return this.records; }
};
window.GameForgeLicenceCommercialTracker = GameForgeLicenceCommercialTracker;
