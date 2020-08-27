async function drawScorigamiViz() {

    // 1. Access data
  const dataset = await d3.json("../../datafile.json")

  console.table(dataset)

}

drawScorigamiViz()