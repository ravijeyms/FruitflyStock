import { initializeGoogleSheets } from "./google-picker.js";
import { initializeSearch } from "./search.js";
import { initializeSelection } from "./selection.js";

const selection = initializeSelection();
const search = initializeSearch(selection);

initializeGoogleSheets(search.refresh);
